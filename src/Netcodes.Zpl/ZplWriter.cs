using System.Text;
using Netcodes.Template;

namespace Netcodes.Zpl;

/// <summary>Turns template elements into ZPL text. One instance writes one label.</summary>
internal sealed class ZplWriter
{
    private readonly StringBuilder _sb = new();
    private readonly int _dpi;
    private readonly int _labelHeightDots;
    private readonly List<string> _warnings;

    public ZplWriter(LabelTemplate template, List<string> warnings)
    {
        _dpi = template.Dpi;
        _labelHeightDots = Dots.FromMm(template.HeightMm, template.Dpi);
        _warnings = warnings;
    }

    /// <summary>Writes the label start, UTF-8 selection, size, home position and any requested printer settings.</summary>
    public void Header(LabelTemplate t, GenerateOptions? options)
    {
        _sb.Append("^XA\n");
        // ^JUR reloads the printer's saved configuration, which overrides every setting written after it, so a recall
        // must come before this label's own settings (a save, in contrast, goes last and stores them).
        if (options?.ConfigUpdate == ConfigUpdate.Recall) _sb.Append("^JUR\n");
        _sb.Append("^CI28\n");
        _sb.Append("^PW").Append(Dots.FromMm(t.WidthMm, t.Dpi)).Append('\n');
        _sb.Append("^LL").Append(_labelHeightDots).Append('\n');
        _sb.Append("^LH0,0\n");
        if (options?.PrintSpeed is { } speed) _sb.Append("^PR").Append(speed).Append('\n');
        if (options?.Darkness is { } darkness) _sb.Append("^MD").Append(darkness).Append('\n');
        if (options?.Media is { } media) _sb.Append("^MT").Append(media == PrinterMedia.Thermal ? 'T' : 'D').Append('\n');
        // The letters follow the order of the PrintMode and MediaTracking enums.
        if (options?.Mode is { } mode) _sb.Append("^MM").Append("TPRACD"[(int)mode]).Append('\n');
        if (options?.Tracking is { } tracking) _sb.Append("^MN").Append("NYMA"[(int)tracking]).Append('\n');
        if (options?.Inverted is { } inverted) _sb.Append("^PO").Append(inverted ? 'I' : 'N').Append('\n');
        if (options?.LabelShiftDots is { } shift) _sb.Append("^LS").Append(shift).Append('\n');
        // Last: a save stores the settings written above it (a recall goes first, see above).
        if (options?.ConfigUpdate == ConfigUpdate.Save) _sb.Append("^JUS\n");
    }

    /// <summary>Writes the quantity (if requested) and the label end.</summary>
    public void Footer(GenerateOptions? options)
    {
        if (options?.Quantity is { } quantity) _sb.Append("^PQ").Append(quantity).Append('\n');
        _sb.Append("^XZ\n");
    }

    /// <summary>Writes one element; <paramref name="originX"/>/<paramref name="originY"/> shift it (used by repeat rows).</summary>
    public void Write(LabelElement element, DataContext data, int originX, int originY)
    {
        // Repeat rows are counted through their children, so nested repeats share one budget.
        if (element is not RepeatElement && !TakeFieldBudget()) return;
        switch (element)
        {
            case TextElement text: WriteText(text, data, originX, originY); break;
            case BoxElement box: WriteBox(box, originX, originY); break;
            case BarcodeElement barcode: WriteBarcode(barcode, data, originX, originY); break;
            case QrElement qr: WriteQr(qr, data, originX, originY); break;
            case Gs1Element gs1: WriteGs1(gs1, data, originX, originY); break;
            case RepeatElement repeat: WriteRepeat(repeat, data, originX, originY); break;
            case ImageElement image: WriteImage(image, data, originX, originY); break;
            default:
                _warnings.Add($"An element of type {element.GetType().Name} cannot be drawn yet and was left out.");
                break;
        }
    }

    public override string ToString() => _sb.ToString();

    // Why a cap: a row height that rounds to 0 dots never runs off the label, so a long list (or repeats nested
    // inside repeats) could otherwise grow the ZPL without bound and exhaust memory on the server.
    private const int MaxFields = 5000;
    private int _fieldsWritten;
    private bool _budgetWarned;

    /// <summary>Counts one more field; returns false (and warns once) when the label already has too many.</summary>
    private bool TakeFieldBudget()
    {
        if (_fieldsWritten < MaxFields)
        {
            _fieldsWritten++;
            return true;
        }
        if (!_budgetWarned)
        {
            _budgetWarned = true;
            _warnings.Add($"The label would have too many fields (more than {MaxFields}), so the rest were left out. Check your repeat settings and list sizes.");
        }
        return false;
    }

    private int D(double mm) => Dots.FromMm(mm, _dpi);

    private string At(LabelElement e, int ox, int oy) => $"^FO{ox + D(e.X)},{oy + D(e.Y)}";

    // One mapping used both to check the data and (through the command switch) to write it, so they cannot drift apart.
    private static ZplSymbology ToZpl(Symbology s) => s switch
    {
        Symbology.Code39 => ZplSymbology.Code39,
        Symbology.Ean13 => ZplSymbology.Ean13,
        Symbology.UpcA => ZplSymbology.UpcA,
        Symbology.Itf => ZplSymbology.Itf,
        Symbology.DataMatrix => ZplSymbology.DataMatrix,
        Symbology.Code93 => ZplSymbology.Code93,
        Symbology.Codabar => ZplSymbology.Codabar,
        Symbology.Msi => ZplSymbology.Msi,
        Symbology.Aztec => ZplSymbology.Aztec,
        Symbology.Pdf417 => ZplSymbology.Pdf417,
        _ => ZplSymbology.Code128,
    };

    private static char Orientation(int rotation) => rotation switch { 90 => 'R', 180 => 'I', 270 => 'B', _ => 'N' };

    /// <summary>Appends <c>^FD...^FS</c>, adding <c>^FH_</c> first when the text needed hex escapes.</summary>
    private void FieldData(string text)
    {
        var (escaped, needsHex) = ZplEscape.Escape(text);
        if (needsHex) _sb.Append("^FH_");
        _sb.Append("^FD").Append(escaped).Append("^FS\n");
    }

    private void WriteText(TextElement t, DataContext data, int ox, int oy)
    {
        var text = data.Expand(t.Text, _warnings);
        var height = D(t.HeightMm);
        var width = t.WidthMm is { } w ? D(w) : height;
        var font = t.Font.Length == 1 && char.IsAsciiLetterOrDigit(t.Font[0]) ? t.Font : "0";
        _sb.Append(At(t, ox, oy));
        if (t.MaxWidthMm is { } maxWidth)
            _sb.Append("^FB").Append(D(maxWidth)).Append(',').Append(Math.Max(1, t.MaxLines)).Append(",0,L,0");
        _sb.Append("^A").Append(font).Append(Orientation(t.Rotation)).Append(',').Append(height).Append(',').Append(width);
        FieldData(text);
    }

    private void WriteBarcode(BarcodeElement b, DataContext data, int ox, int oy)
    {
        var content = data.Expand(b.Data, _warnings);
        if (string.IsNullOrEmpty(content))
        {
            _warnings.Add("A barcode has no data, so it was left out of the label. Check the barcode's data field.");
            return;
        }
        // Check the data now, so a caller who only generates and prints still hears about unusable data.
        var problem = ZXingBarcodeEncoder.Check(ToZpl(b.Symbology), content);
        if (problem is not null)
        {
            _warnings.Add(problem + " The barcode was left out of the label.");
            return;
        }
        var height = D(b.HeightMm);
        var module = Math.Clamp(b.ModuleWidth, 1, 10);
        var o = Orientation(b.Rotation);
        var text = b.ShowText ? 'Y' : 'N';
        if (b.Symbology == Symbology.Pdf417)
        {
            // ^B7 wants the height of ONE row, but the template gives the symbol's overall height, so encode once to
            // learn the row count. Never thinner than 3 modules per row: thinner rows do not scan.
            if (!_barcodeBudget.TryTake(ZplSymbology.Pdf417, content.Length))
            {
                if (_barcodeBudget.WarningOnce() is { } warning) _warnings.Add(warning);
                return;
            }
            BarcodeMatrix matrix;
            try
            {
                matrix = new ZXingBarcodeEncoder().Encode(ZplSymbology.Pdf417, content);
            }
            catch (BarcodeException ex)
            {
                // Check() only looks at the characters; whether the data fits in a PDF417 is only known by encoding.
                _warnings.Add(ex.Message + " The barcode was left out of the label.");
                return;
            }
            var rowHeight = Math.Max(module * 3, height / Math.Max(1, matrix.Height));
            // Write the column count the preview chose (a PDF417 is 69 modules of framing plus 17 per data column), so
            // the printed symbol has the same shape instead of whatever the printer picks when the count is blank.
            var columns = Math.Clamp((matrix.Width - 69) / 17, 1, 30);
            _sb.Append($"^BY{module},3,{rowHeight}").Append(At(b, ox, oy)).Append($"^B7{o},{rowHeight},0,{columns},,N");
            FieldData(content);
            return;
        }
        // Codabar: the start and stop letters belong in the command's parameters, not in the data (the data may only hold
        // digits and - $ : / . +), so a framed value such as B1234D is split here.
        var codabar = ZXingBarcodeEncoder.SplitCodabar(content);
        if (b.Symbology == Symbology.Codabar) content = codabar.Body;
        var command = b.Symbology switch
        {
            // Mode A (automatic), not N: in mode N a ">" followed by a character is a printer command (">5" switches
            // character set, ">8" is FNC1), so SKU>5123 would print as something else than the preview shows.
            Symbology.Code128 => $"^BC{o},{height},{text},N,N,A",
            Symbology.Code39 => $"^B3{o},N,{height},{text},N",
            Symbology.Ean13 => $"^BE{o},{height},{text},N",
            Symbology.UpcA => $"^BU{o},{height},{text},N,Y",
            Symbology.Itf => $"^B2{o},{height},{text},N,N",
            // For Data Matrix the ZPL "height" parameter is the size of one module in dots.
            Symbology.DataMatrix => $"^BX{o},{module},200",
            Symbology.Code93 => $"^BA{o},{height},{text},N,N",
            Symbology.Codabar => $"^BK{o},N,{height},{text},N,{codabar.Start},{codabar.Stop}",
            // Check digit letter: A = none, B = Mod 10. ZXing (the preview) draws no check digit, so ask the printer for none.
            Symbology.Msi => $"^BM{o},A,{height},{text},N,N",
            // Aztec: magnification, no extended channel, default error correction, not a menu symbol, one symbol.
            Symbology.Aztec => $"^BO{o},{module},N,0,N,1",
            _ => throw new NotSupportedException($"The barcode type {b.Symbology} is not supported."),
        };
        // ^BY sets the module width used by the barcode command that follows it.
        _sb.Append($"^BY{module},3,{height}").Append(At(b, ox, oy)).Append(command);
        FieldData(content);
    }

    private void WriteQr(QrElement q, DataContext data, int ox, int oy)
    {
        var content = data.Expand(q.Data, _warnings);
        if (string.IsNullOrEmpty(content))
        {
            _warnings.Add("A QR code has no data, so it was left out of the label. Check the QR code's data field.");
            return;
        }
        var level = q.ErrorCorrection?.Trim().ToUpperInvariant();
        if (level is not ("L" or "M" or "Q" or "H"))
        {
            _warnings.Add($"The QR error correction \"{q.ErrorCorrection}\" is not valid. Use L, M, Q or H. Q was used instead.");
            level = "Q";
        }
        var size = Math.Clamp(q.Size, 1, 10);
        // Model 2 QR; the field data starts with <error correction><input mode A = automatic>, then a comma.
        _sb.Append(At(q, ox, oy)).Append($"^BQ{Orientation(q.Rotation)},2,{size}");
        FieldData($"{level}A,{content}");
    }

    private void WriteGs1(Gs1Element g, DataContext data, int ox, int oy)
    {
        var fields = g.Fields.ToDictionary(kv => kv.Key, kv => data.Expand(kv.Value ?? "", _warnings));
        string content;
        try
        {
            content = Gs1.Build(fields, _warnings);
        }
        catch (TemplateException ex)
        {
            // One unusable GS1 value must not abort the whole label: drop this barcode and say why.
            _warnings.Add($"The GS1 barcode was left out of the label. {ex.Message}");
            return;
        }
        var height = D(g.HeightMm);
        var module = Math.Clamp(g.ModuleWidth, 1, 10);
        // Mode D is ZPL's GS1-128 mode: the printer drops the brackets, inserts FNC1 and prints "(00)..." itself.
        _sb.Append($"^BY{module},3,{height}").Append(At(g, ox, oy)).Append($"^BC{Orientation(g.Rotation)},{height},Y,N,N,D");
        FieldData(content);
    }

    // One budget for every barcode that has to be encoded while writing (PDF417), repeat rows included.
    private readonly BarcodeWorkBudget _barcodeBudget = new();

    // One budget for every picture of the label, repeat rows included (see ImageBudget).
    private readonly ImageBudget _imageBudget = new();

    private void WriteImage(ImageElement i, DataContext data, int ox, int oy)
    {
        var source = data.Expand(i.Data, _warnings);
        try
        {
            var gf = ImageEncoder.ToGraphicField(source, D(i.WidthMm), i.HeightMm is { } h ? D(h) : null,
                Math.Clamp(i.Threshold, 0, 255), i.Invert, i.Rotation, _imageBudget);
            _sb.Append(At(i, ox, oy)).Append(gf).Append("^FS\n");
        }
        catch (ImageException ex)
        {
            // One unusable picture must not stop the label: skip it and say why.
            _warnings.Add($"An image was left out of the label. {ex.Message}");
        }
        catch (Exception)
        {
            // The picture library reports a failed allocation (a very large picture on a busy machine) as a plain
            // Exception. Whatever it was, the rest of the label is still good.
            _warnings.Add("An image was left out of the label because it could not be processed (it may be too large for the memory available). Use a smaller picture.");
        }
    }

    private void WriteRepeat(RepeatElement r, DataContext data, int ox, int oy)
    {
        var items = data.GetArray(r.Source, _warnings);
        var rowHeight = D(r.RowHeightMm);
        var baseX = ox + D(r.X);
        var baseY = oy + D(r.Y);
        var rows = Math.Min(items.Count, Math.Max(1, r.MaxRows));
        for (var i = 0; i < rows; i++)
        {
            // Every row costs one unit of the label's budget, even a row whose children write nothing: repeats nested
            // inside repeats multiply, and a repeat with no children would otherwise loop without ever being counted.
            if (!TakeFieldBudget()) return;
            var rowY = baseY + i * rowHeight;
            // A row that would run off the label would print garbage on the next label, so stop and say so.
            if (rowY + rowHeight > _labelHeightDots)
            {
                _warnings.Add($"Only {i} of {items.Count} rows fit on the label; the rest were left out.");
                return;
            }
            var rowData = data.ForItem(items[i], i);
            foreach (var child in r.Elements)
                Write(child, rowData, baseX, rowY);
        }
        if (items.Count > rows)
            _warnings.Add($"The list has {items.Count} rows but the repeat has a limit of {rows}; the rest were left out.");
    }

    private void WriteBox(BoxElement b, int ox, int oy)
    {
        var width = D(b.WidthMm);
        var height = D(b.HeightMm);
        // ZPL fills a box when the border is at least as thick as the box, so a filled block is just a huge border.
        // Why Min: ZPL raises a width or height that is smaller than the thickness up to the thickness, so using the
        // larger side would turn a thin filled block (a line) into a big square. The smaller side already fills it.
        var thickness = b.Filled ? Math.Max(1, Math.Min(width, height)) : Math.Max(1, D(b.BorderMm));
        _sb.Append(At(b, ox, oy)).Append("^GB").Append(width).Append(',').Append(height).Append(',').Append(thickness).Append("^FS\n");
    }
}
