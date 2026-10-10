using SkiaSharp;

namespace Netcodes.Zpl;

/// <summary>Draws parsed ZPL operations onto a canvas in printer dots (1 dot = 1 pixel).</summary>
internal sealed class Painter
{
    // Zebra's font height is roughly the capital-letter height; DejaVu's capitals are 0.73 of its em size.
    private const float CapHeightRatio = 0.73f;

    // Why a limit: ZPL can come from an API caller, and encoding megabytes of barcode data would tie up the server.
    // 8000 characters is above what any real barcode symbology can hold.
    private const int MaxBarcodeDataLength = 8000;
    private const int MaxTextLength = 5000;

    // One budget for every barcode of the label (see BarcodeWorkBudget).
    private readonly BarcodeWorkBudget _barcodeBudget = new();
    private readonly SKCanvas _canvas;
    private readonly IBarcodeEncoder _encoder;
    private readonly List<string> _warnings;

    public Painter(SKCanvas canvas, IBarcodeEncoder encoder, List<string> warnings)
    {
        _canvas = canvas;
        _encoder = encoder;
        _warnings = warnings;
    }

    public void Draw(ZplOp op)
    {
        switch (op)
        {
            case TextOp t: DrawText(t); break;
            case BoxOp b: DrawBox(b); break;
            case CircleOp c: DrawCircle(c); break;
            case BarcodeOp bc: DrawBarcode(bc); break;
            case ImageOp img: DrawImage(img); break;
        }
    }

    private void DrawImage(ImageOp img)
    {
        if (img.WidthDots == 0 || img.HeightDots == 0) return;
        try
        {
            DrawImageBits(img);
        }
        catch (Exception)
        {
            // A failed allocation inside the picture library must not abort the preview of the rest of the label.
            _warnings.Add("A graphic could not be drawn (it may be too large for the memory available), so it was left out.");
        }
    }

    private void DrawImageBits(ImageOp img)
    {
        // Alpha8 holds one byte per dot and takes its colour from the paint, so the biggest allowed graphic costs
        // 16 MB, and reverse printing (the Difference blend) works like it does for boxes.
        using var mask = new SKBitmap(new SKImageInfo(img.WidthDots, img.HeightDots, SKColorType.Alpha8, SKAlphaType.Premul));
        var pixels = new byte[mask.RowBytes * img.HeightDots];
        for (var y = 0; y < img.HeightDots; y++)
            for (var x = 0; x < img.WidthDots; x++)
                if ((img.Bits[y * img.BytesPerRow + x / 8] & (0x80 >> (x % 8))) != 0)
                    pixels[y * mask.RowBytes + x] = 255;
        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, mask.GetPixels(), pixels.Length);
        using var paint = Fill(img.Reverse);
        using var image = SKImage.FromBitmap(mask);
        // ^FT puts the origin at the bottom-left of the graphic.
        _canvas.DrawImage(image, img.X, img.Baseline ? img.Y - img.HeightDots : img.Y, new SKSamplingOptions(SKFilterMode.Nearest), paint);
    }

    // Printed black is the default; ^FR draws white with the Difference blend so it flips whatever is underneath.
    private static SKPaint Fill(bool reverse, bool black = true) => new()
    {
        Color = reverse || !black ? SKColors.White : SKColors.Black,
        BlendMode = reverse ? SKBlendMode.Difference : SKBlendMode.SrcOver,
        IsAntialias = false,
        Style = SKPaintStyle.Fill,
    };

    /// <summary>
    /// Runs <paramref name="draw"/> in a coordinate system where (0,0) is the top-left of the unrotated field box,
    /// positioned so that the rotated field's visual top-left corner lands on (x, y) as ZPL's ^FO does.
    /// </summary>
    private void WithOrientation(int x, int y, int w, int h, char orientation, Action draw)
    {
        _canvas.Save();
        switch (orientation)
        {
            case 'R': _canvas.Translate(x + h, y); _canvas.RotateDegrees(90); break;
            case 'I': _canvas.Translate(x + w, y + h); _canvas.RotateDegrees(180); break;
            case 'B': _canvas.Translate(x, y + w); _canvas.RotateDegrees(270); break;
            default: _canvas.Translate(x, y); break;
        }
        draw();
        _canvas.Restore();
    }

    /// <summary>
    /// How far ^FT moves a field compared with ^FO. ^FO is the field's top-left corner; ^FT is the START of the field
    /// on its baseline (the bottom-left of bars or a text line), and the field then runs in its own direction from
    /// there: right for N, down for R, left for I, up for B (checked against Labelary's output). <paramref name="length"/>
    /// is the extent along the text direction and <paramref name="across"/> the height (letters or bars) above the baseline.
    /// </summary>
    private static (int X, int Y) BaselineShift(char orientation, int length, int across) => orientation switch
    {
        'R' => (0, 0),
        'I' => (-length, 0),
        'B' => (-across, -length),
        _ => (0, -across),
    };

    private void DrawText(TextOp t)
    {
        var height = Math.Max(1, t.HeightDots);
        var size = height / CapHeightRatio;
        // Font 0 scales to the requested width; the fixed fonts A-H advance by the requested cell width.
        var bitmapFont = FontCache.For(t.Font) == FontCache.Fixed;
        var scaleX = bitmapFont
            ? (t.WidthDots > 0 ? t.WidthDots : height * 0.6f) / (0.602f * size)
            : (t.WidthDots > 0 ? t.WidthDots / (float)height : 1f);
        using var font = new SKFont(FontCache.For(t.Font), size) { ScaleX = scaleX, Edging = SKFontEdging.Alias };

        var text = t.Text;
        if (text.Length > MaxTextLength)
        {
            _warnings.Add($"A text field is too long ({text.Length} characters; the most the preview draws is {MaxTextLength}), so it was cut off.");
            text = text[..MaxTextLength];
        }
        var lines = t.Block is { } block ? Wrap(text, block, font) : new List<string> { text };
        // A printer prints text that does not fit in the block on top of the last line instead of dropping it, so the
        // preview does the same: lines past the limit all share the last visible row.
        var maxRows = t.Block is { MaxLines: > 0 } mb ? mb.MaxLines : int.MaxValue;
        var rows = Math.Min(lines.Count, maxRows);
        var lineHeight = height + (t.Block?.LineSpacing ?? 0);
        var boxWidth = t.Block is { } b ? b.WidthDots : lines.Max(l => font.MeasureText(l));
        var boxHeight = height + (rows - 1) * lineHeight;
        using var paint = Fill(t.Reverse);

        var textWidth = (int)Math.Ceiling(boxWidth);
        var (shiftX, shiftY) = t.Baseline ? BaselineShift(t.Orientation, textWidth, height) : (0, 0);
        WithOrientation(t.X + shiftX, t.Y + shiftY, textWidth, boxHeight, t.Orientation, () =>
        {
            for (var i = 0; i < lines.Count; i++)
            {
                var lineWidth = font.MeasureText(lines[i]);
                var x = t.Block?.Justify switch { 'C' => (boxWidth - lineWidth) / 2, 'R' => boxWidth - lineWidth, _ => 0f };
                _canvas.DrawText(lines[i], x, height + Math.Min(i, rows - 1) * lineHeight, SKTextAlign.Left, font, paint);
            }
        });
    }

    private static List<string> Wrap(string text, ZplBlock block, SKFont font)
    {
        var lines = new List<string>();
        var current = new System.Text.StringBuilder();
        // Widths are added up word by word instead of re-measuring the growing line, which keeps wrapping linear.
        var currentWidth = 0f;
        var spaceWidth = font.MeasureText(" ");
        foreach (var word in text.Split(' '))
        {
            var wordWidth = font.MeasureText(word);
            if (current.Length > 0 && block.WidthDots > 0 && currentWidth + spaceWidth + wordWidth > block.WidthDots)
            {
                lines.Add(current.ToString());
                current.Clear().Append(word);
                currentWidth = wordWidth;
            }
            else
            {
                if (current.Length > 0) { current.Append(' '); currentWidth += spaceWidth; }
                current.Append(word);
                currentWidth += wordWidth;
            }
        }
        lines.Add(current.ToString());
        return lines;
    }

    private void DrawBox(BoxOp b)
    {
        using var paint = Fill(b.Reverse, b.Black);
        var t = Math.Min(b.Thickness, Math.Min(b.WidthDots, b.HeightDots));
        // A border at least half the smaller side leaves no hollow, so draw one solid rectangle.
        if (t * 2 >= Math.Min(b.WidthDots, b.HeightDots))
        {
            _canvas.DrawRect(b.X, b.Y, b.WidthDots, b.HeightDots, paint);
            return;
        }
        // Four separate strips (not one stroked rectangle) so reverse printing never double-flips the corners.
        _canvas.DrawRect(b.X, b.Y, b.WidthDots, t, paint);
        _canvas.DrawRect(b.X, b.Y + b.HeightDots - t, b.WidthDots, t, paint);
        _canvas.DrawRect(b.X, b.Y + t, t, b.HeightDots - 2 * t, paint);
        _canvas.DrawRect(b.X + b.WidthDots - t, b.Y + t, t, b.HeightDots - 2 * t, paint);
    }

    private void DrawCircle(CircleOp c)
    {
        using var paint = Fill(c.Reverse, c.Black);
        var r = c.Diameter / 2f;
        if (c.Thickness * 2 >= c.Diameter)
        {
            _canvas.DrawCircle(c.X + r, c.Y + r, r, paint);
            return;
        }
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = c.Thickness;
        _canvas.DrawCircle(c.X + r, c.Y + r, r - c.Thickness / 2f, paint);
    }

    private void DrawBarcode(BarcodeOp b)
    {
        if (b.Data.Length > MaxBarcodeDataLength)
        {
            _warnings.Add($"A barcode's data is too long ({b.Data.Length} characters; the most the preview draws is {MaxBarcodeDataLength}), so it was left out.");
            return;
        }
        if (!_barcodeBudget.TryTake(b.Symbology, b.Data.Length))
        {
            if (_barcodeBudget.WarningOnce() is { } warning) _warnings.Add(warning);
            return;
        }
        BarcodeMatrix matrix;
        try
        {
            matrix = _encoder.Encode(b.Symbology, b.Data, b.QrErrorCorrection,
                new BarcodeOptions(b.WideToNarrowRatio, b.Pdf417Security, b.Pdf417Columns));
        }
        catch (BarcodeException ex)
        {
            // Skip just this barcode; a missing code on the preview plus this message is the useful outcome.
            _warnings.Add(ex.Message);
            return;
        }

        var module = Math.Max(1, b.ModuleWidth);
        using var paint = Fill(b.Reverse);

        if (b.Symbology is ZplSymbology.QrCode or ZplSymbology.DataMatrix or ZplSymbology.Aztec)
        {
            var (sx, sy) = b.Baseline ? BaselineShift(b.Orientation, matrix.Width * module, matrix.Height * module) : (0, 0);
            WithOrientation(b.X + sx, b.Y + sy, matrix.Width * module, matrix.Height * module, b.Orientation,
                () => DrawModules(matrix, module, module, 0, paint));
            return;
        }

        if (b.Symbology == ZplSymbology.Pdf417)
        {
            // Rows are b.HeightDots tall (^B7); without one, 3 modules is the usual minimum row height.
            var rowHeight = b.HeightDots > 0 ? b.HeightDots : module * 3;
            var (sx, sy) = b.Baseline ? BaselineShift(b.Orientation, matrix.Width * module, matrix.Height * rowHeight) : (0, 0);
            WithOrientation(b.X + sx, b.Y + sy, matrix.Width * module, matrix.Height * rowHeight, b.Orientation,
                () => DrawModules(matrix, module, rowHeight, 0, paint));
            return;
        }

        var barHeight = Math.Max(1, b.HeightDots);
        var captionSize = b.ShowText ? Math.Max(14, module * 8) : 0;
        var width = matrix.Width * module;
        // The field's box is the bars (plus a caption that sits above them). A caption below the bars lies OUTSIDE the
        // box, on whichever side "below" turns into when the field is rotated: that is where Labelary and printers put
        // it, so the bars stay where ^FO / ^FT say regardless of the interpretation line.
        var boxHeight = barHeight + (b.TextAbove ? captionSize : 0);
        var (shiftX, shiftY) = b.Baseline ? BaselineShift(b.Orientation, width, boxHeight) : (0, 0);
        WithOrientation(b.X + shiftX, b.Y + shiftY, width, boxHeight, b.Orientation, () =>
        {
            var barTop = b.TextAbove ? captionSize : 0;
            DrawModules(matrix, module, barHeight, barTop, paint);
            if (!b.ShowText) return;
            using var font = new SKFont(FontCache.Fixed, captionSize * 0.8f) { Edging = SKFontEdging.Alias };
            var textWidth = font.MeasureText(b.Data);
            var baseline = b.TextAbove ? captionSize * 0.85f : barHeight + captionSize * 0.85f;
            _canvas.DrawText(b.Data, (width - textWidth) / 2, baseline, SKTextAlign.Left, font, paint);
        });
    }

    /// <summary>Draws each row of the matrix as runs of dark modules.</summary>
    private void DrawModules(BarcodeMatrix matrix, int moduleWidth, int rowHeight, int top, SKPaint paint)
    {
        for (var y = 0; y < matrix.Height; y++)
        {
            var x = 0;
            while (x < matrix.Width)
            {
                if (!matrix[x, y]) { x++; continue; }
                var start = x;
                while (x < matrix.Width && matrix[x, y]) x++;
                _canvas.DrawRect(start * moduleWidth, top + y * rowHeight, (x - start) * moduleWidth, rowHeight, paint);
            }
        }
    }
}
