using SkiaSharp;

namespace Netcodes.Zpl;

/// <summary>Settings for <see cref="ZplRenderer"/>.</summary>
public sealed class RenderOptions
{
    /// <summary>Width used when the ZPL has no <c>^PW</c> (4 inches at 203 dpi).</summary>
    public int DefaultWidthDots { get; init; } = 812;
    /// <summary>Length used when the ZPL has no <c>^LL</c> (6 inches at 203 dpi).</summary>
    public int DefaultHeightDots { get; init; } = 1218;
    /// <summary>Largest width or length the preview will draw; guards against absurd values exhausting memory.</summary>
    public int MaxDots { get; init; } = 4000;
    /// <summary>The barcode encoder to use.</summary>
    public IBarcodeEncoder Encoder { get; init; } = new ZXingBarcodeEncoder();
}

/// <summary>A rendered preview.</summary>
/// <param name="Png">The label as a PNG image, one pixel per printer dot.</param>
/// <param name="WidthDots">Image width.</param>
/// <param name="HeightDots">Image height.</param>
/// <param name="Warnings">Plain-language notes, for example commands that were skipped.</param>
public sealed record RenderResult(byte[] Png, int WidthDots, int HeightDots, IReadOnlyList<string> Warnings);

/// <summary>Draws ZPL as a PNG preview.</summary>
public static class ZplRenderer
{

    /// <summary>Renders the first label in <paramref name="zpl"/>. Never throws for bad ZPL; problems become warnings.</summary>
    /// <param name="zpl">The ZPL to draw.</param>
    /// <param name="options">Optional settings.</param>
    public static RenderResult Render(string zpl, RenderOptions? options = null)
    {
        options ??= new RenderOptions();
        var doc = ZplParser.Parse(zpl);
        var warnings = new List<string>(doc.Warnings);

        var width = doc.WidthDots ?? options.DefaultWidthDots;
        var height = doc.HeightDots ?? options.DefaultHeightDots;
        if (width < 1 || height < 1 || width > options.MaxDots || height > options.MaxDots)
        {
            warnings.Add($"The label size {width} x {height} dots is outside what the preview can draw (1 to {options.MaxDots}). " +
                         "The default size was used. Check the ^PW and ^LL values.");
            width = options.DefaultWidthDots;
            height = options.DefaultHeightDots;
        }

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        var painter = new Painter(canvas, options.Encoder, warnings);
        // The parser already caps the number of fields (and the input size), so nothing here can grow without bound.
        foreach (var op in doc.Ops)
            painter.Draw(op);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return new RenderResult(data.ToArray(), width, height, warnings);
    }
}
