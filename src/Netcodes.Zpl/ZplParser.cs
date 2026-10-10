using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Netcodes.Zpl;

/// <summary>
/// Reads ZPL into drawing operations. It is deliberately forgiving: a command it does not know is skipped with
/// a warning, because a preview that shows most of the label is far more useful than an error.
/// </summary>
public static class ZplParser
{
    private const int MaxInputChars = 1_000_000;

    /// <summary>Parses the first label in <paramref name="zpl"/>.</summary>
    /// <param name="zpl">ZPL text; may be empty.</param>
    public static ZplDocument Parse(string zpl)
    {
        var doc = new ZplDocument();
        var state = new ParserState(doc);
        zpl ??= "";
        // Why a limit: ZPL may come from an API caller, and nothing real needs more than a megabyte for one label.
        if (zpl.Length > MaxInputChars)
        {
            zpl = zpl[..MaxInputChars];
            doc.Warnings.Add($"The ZPL is too large to read in full. Only the first {MaxInputChars:N0} characters were used.");
        }
        foreach (var (command, args) in Tokenize(zpl))
        {
            if (!state.Apply(command, args)) break;
        }
        return doc;
    }

    // A command starts at ^ or ~ and runs to the next ^ or ~. Anything outside commands (line breaks) is ignored.
    private static IEnumerable<(string Command, string Args)> Tokenize(string zpl)
    {
        var i = 0;
        while (i < zpl.Length)
        {
            var c = zpl[i];
            if (c != '^' && c != '~') { i++; continue; }
            var start = i + 1;
            if (start >= zpl.Length) yield break;

            string command;
            int argStart;
            // ^A is the only one-letter command: the font letter follows it directly (^A0N,30,30).
            if (c == '^' && char.ToUpperInvariant(zpl[start]) == 'A')
            {
                command = "A";
                argStart = start + 1;
            }
            else
            {
                var length = Math.Min(2, zpl.Length - start);
                command = (c == '~' ? "~" : "") + zpl.Substring(start, length).ToUpperInvariant();
                argStart = start + length;
            }

            var end = zpl.IndexOfAny(new[] { '^', '~' }, argStart);
            if (end < 0) end = zpl.Length;
            yield return (command, zpl[argStart..end]);
            i = end;
        }
    }
}

/// <summary>The state ZPL carries between commands (position, defaults, the field being built).</summary>
internal sealed partial class ParserState
{
    private const int MaxOps = 5000;

    private static readonly HashSet<string> Ignored = new()
    {
        "CI", "PR", "MD", "MM", "MN", "MT", "PQ", "PO", "JU", "LS", "LT", "PM", "MU",
    };

    [GeneratedRegex(@"^(?<ec>[HQML])?(?<mode>[AM]),")]
    private static partial Regex QrPrefix();

    private sealed record BarcodeSpec(ZplSymbology Symbology, char Orientation, int Height, bool ShowText,
        bool TextAbove, int ModuleSize, char Ec, int Security = 0, int Columns = 0, char Start = 'A', char Stop = 'A');

    private readonly ZplDocument _doc;
    private readonly HashSet<string> _warned = new();

    // Persisting state (survives ^FS).
    private int _homeX, _homeY;
    private char _defaultOrientation = 'N';
    private string _defaultFont = "A";
    private int _defaultHeight = 9, _defaultWidth = 5;
    private int _moduleWidth = 2, _barcodeHeight = 10;
    private double _wideRatio = 3.0;
    private bool _finished;
    private long _graphicBytes;

    // Per-field state (cleared by ^FS).
    private int _x, _y;
    private bool _baseline, _reverse, _hexOn;
    private char _hexChar = '_';
    private ZplBlock? _block;
    private (string Font, char Orientation, int Height, int Width)? _fontOverride;
    private BarcodeSpec? _barcode;
    private ZplOp? _shape;
    private string? _data;

    public ParserState(ZplDocument doc) => _doc = doc;

    /// <summary>Applies one command. Returns false when parsing should stop (after the first label).</summary>
    public bool Apply(string command, string args)
    {
        if (_finished)
        {
            if (command == "XA")
                Warn("MORE", "The ZPL contains more than one label. Only the first one is previewed.");
            return false;
        }

        var p = args.Split(',');
        switch (command)
        {
            case "XA": break;
            case "XZ": _finished = true; break;
            case "PW": { var v = Int(p, 0, 0); if (v > 0) _doc.WidthDots = v; break; }
            case "LL": { var v = Int(p, 0, 0); if (v > 0) _doc.HeightDots = v; break; }
            case "LH": _homeX = Int(p, 0, 0); _homeY = Int(p, 1, 0); break;
            case "FO": _x = _homeX + Int(p, 0, 0); _y = _homeY + Int(p, 1, 0); _baseline = false; break;
            case "FT": _x = _homeX + Int(p, 0, 0); _y = _homeY + Int(p, 1, 0); _baseline = true; break;
            case "FW": _defaultOrientation = Chr(p, 0, 'N'); break;
            case "FR": _reverse = true; break;
            case "FH": _hexOn = true; _hexChar = args.Length > 0 && args[0] != ',' ? args[0] : '_'; break;
            case "FB": _block = new ZplBlock(Int(p, 0, 0), Math.Max(1, Int(p, 1, 1)), Int(p, 2, 0), Chr(p, 3, 'L')); break;
            case "A": ApplyFont(args); break;
            case "CF":
                if (p[0].Trim().Length > 0) _defaultFont = p[0].Trim()[..1];
                _defaultHeight = Int(p, 1, _defaultHeight);
                _defaultWidth = Int(p, 2, _defaultWidth);
                break;
            case "GB":
            {
                var t = Math.Max(1, Int(p, 2, 1));
                _shape = new BoxOp
                {
                    WidthDots = Math.Max(Int(p, 0, t), t), HeightDots = Math.Max(Int(p, 1, t), t), Thickness = t,
                    Black = Chr(p, 3, 'B') != 'W', Rounding = Int(p, 4, 0),
                };
                break;
            }
            case "GC":
                _shape = new CircleOp
                {
                    Diameter = Math.Max(3, Int(p, 0, 3)), Thickness = Math.Max(1, Int(p, 1, 1)), Black = Chr(p, 2, 'B') != 'W',
                };
                break;
            case "BY":
                _moduleWidth = Int(p, 0, _moduleWidth);
                _wideRatio = Math.Clamp(Dbl(p, 1, _wideRatio), 2.0, 3.0);
                _barcodeHeight = Int(p, 2, _barcodeHeight);
                break;
            case "BC":
                _barcode = new BarcodeSpec(Chr(p, 5, 'N') == 'D' ? ZplSymbology.Gs1128 : ZplSymbology.Code128,
                    Chr(p, 0, _defaultOrientation), Int(p, 1, _barcodeHeight), Chr(p, 2, 'Y') == 'Y', Chr(p, 3, 'N') == 'Y',
                    _moduleWidth, 'Q');
                break;
            case "B3":
                _barcode = new BarcodeSpec(ZplSymbology.Code39, Chr(p, 0, _defaultOrientation), Int(p, 2, _barcodeHeight),
                    Chr(p, 3, 'Y') == 'Y', Chr(p, 4, 'N') == 'Y', _moduleWidth, 'Q');
                break;
            case "BE":
                _barcode = new BarcodeSpec(ZplSymbology.Ean13, Chr(p, 0, _defaultOrientation), Int(p, 1, _barcodeHeight),
                    Chr(p, 2, 'Y') == 'Y', Chr(p, 3, 'N') == 'Y', _moduleWidth, 'Q');
                break;
            case "BU":
                _barcode = new BarcodeSpec(ZplSymbology.UpcA, Chr(p, 0, _defaultOrientation), Int(p, 1, _barcodeHeight),
                    Chr(p, 2, 'Y') == 'Y', Chr(p, 3, 'N') == 'Y', _moduleWidth, 'Q');
                break;
            case "B2":
                _barcode = new BarcodeSpec(ZplSymbology.Itf, Chr(p, 0, _defaultOrientation), Int(p, 1, _barcodeHeight),
                    Chr(p, 2, 'Y') == 'Y', Chr(p, 3, 'N') == 'Y', _moduleWidth, 'Q');
                break;
            case "BQ":
                _barcode = new BarcodeSpec(ZplSymbology.QrCode, Chr(p, 0, _defaultOrientation), 0, false, false,
                    Math.Max(1, Int(p, 2, 2)), Chr(p, 3, 'Q'));
                break;
            case "BX":
                _barcode = new BarcodeSpec(ZplSymbology.DataMatrix, Chr(p, 0, _defaultOrientation), 0, false, false,
                    Math.Max(1, Int(p, 1, _moduleWidth)), 'Q');
                break;
            case "BA":
                _barcode = new BarcodeSpec(ZplSymbology.Code93, Chr(p, 0, _defaultOrientation), Int(p, 1, _barcodeHeight),
                    Chr(p, 2, 'Y') == 'Y', Chr(p, 3, 'N') == 'Y', _moduleWidth, 'Q');
                break;
            case "BK":
                _barcode = new BarcodeSpec(ZplSymbology.Codabar, Chr(p, 0, _defaultOrientation), Int(p, 2, _barcodeHeight),
                    Chr(p, 3, 'Y') == 'Y', Chr(p, 4, 'N') == 'Y', _moduleWidth, 'Q',
                    Start: Chr(p, 5, 'A'), Stop: Chr(p, 6, 'A'));
                break;
            case "BM":
                _barcode = new BarcodeSpec(ZplSymbology.Msi, Chr(p, 0, _defaultOrientation), Int(p, 2, _barcodeHeight),
                    Chr(p, 3, 'Y') == 'Y', Chr(p, 4, 'N') == 'Y', _moduleWidth, 'Q');
                break;
            case "BO":
                // Aztec's second parameter is the magnification (module size in dots), like QR.
                _barcode = new BarcodeSpec(ZplSymbology.Aztec, Chr(p, 0, _defaultOrientation), 0, false, false,
                    Math.Max(1, Int(p, 1, _moduleWidth)), 'Q');
                break;
            case "GF":
                _shape = ParseGraphic(args);
                break;
            case "B7":
                // PDF417: the second parameter is the height of one row, not of the symbol.
                _barcode = new BarcodeSpec(ZplSymbology.Pdf417, Chr(p, 0, _defaultOrientation), Int(p, 1, 0), false, false,
                    _moduleWidth, 'Q', Security: Math.Clamp(Int(p, 2, 0), 0, 8), Columns: Math.Clamp(Int(p, 3, 0), 0, 30));
                break;
            case "FD":
            {
                var raw = args.TrimEnd('\r', '\n');
                _data = _hexOn ? DecodeHex(raw, _hexChar) : raw;
                break;
            }
            case "FS": Commit(); break;
            default:
                if (!Ignored.Contains(command))
                    Warn(command, $"The command {(command.StartsWith('~') ? command : "^" + command)} is not supported yet, so it was skipped.");
                break;
        }
        return true;
    }

    // ^GFa,b,c,d,data: format, bytes in data, total bytes, bytes per row, then the data. The data can itself contain
    // commas (the "rest of the row is zero" shorthand), so only the first four commas are separators.
    private ImageOp? ParseGraphic(string args)
    {
        var parts = args.Split(',', 5);
        var format = parts[0].Trim().ToUpperInvariant();
        if (format != "A")
        {
            Warn("GF", "A graphic uses a data format other than ASCII hex (^GFA), which is not supported yet, so the graphic was skipped.");
            return null;
        }
        if (parts.Length < 5)
        {
            Warn("GF", "A graphic (^GF) has no picture data, so the graphic was skipped.");
            return null;
        }
        if (!int.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var total)
            || !int.TryParse(parts[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var bytesPerRow))
        {
            Warn("GF", "A graphic (^GF) has sizes that are not numbers, so the graphic was skipped.");
            return null;
        }
        // The declared total is what gets allocated (missing rows are blank), so it is what the label's budget spends.
        if (total > 0 && _graphicBytes + total > GraphicField.MaxTotalBytes)
        {
            Warn("GFBUDGET", $"The label has too many graphics (more than {GraphicField.MaxTotalBytes:N0} bytes of picture data in total), so the rest were skipped.");
            return null;
        }
        var bits = GraphicField.Decode(parts[4], bytesPerRow, total, out var problem);
        if (bits is null)
        {
            Warn("GF", problem!);
            return null;
        }
        _graphicBytes += bits.Length;
        return new ImageOp { BytesPerRow = bytesPerRow, Bits = bits };
    }

    private void ApplyFont(string args)
    {
        if (args.Length == 0) return;
        var rest = args[1..].Split(',');
        _fontOverride = (args[..1], Chr(rest, 0, _defaultOrientation), Int(rest, 1, _defaultHeight), Int(rest, 2, 0));
    }

    private void Commit()
    {
        ZplOp? op = null;
        if (_shape is not null)
            op = _shape with { X = _x, Y = _y, Baseline = _baseline, Reverse = _reverse };
        else if (_barcode is { } spec && _data is not null)
            op = MakeBarcode(spec, _data);
        else if (_data is not null)
            op = MakeText(_data);
        if (op is not null)
        {
            if (_doc.Ops.Count < MaxOps) _doc.Ops.Add(op);
            else Warn("OPS", $"The label has too many fields; only the first {MaxOps} were read.");
        }

        _reverse = false; _baseline = false; _hexOn = false; _hexChar = '_';
        _block = null; _fontOverride = null; _barcode = null; _shape = null; _data = null;
    }

    private ZplOp MakeText(string text)
    {
        var (font, orientation, height, width) = _fontOverride ?? (_defaultFont, _defaultOrientation, _defaultHeight, _defaultWidth);
        return new TextOp
        {
            X = _x, Y = _y, Baseline = _baseline, Reverse = _reverse, Text = text, Font = font,
            Orientation = orientation, HeightDots = height, WidthDots = width, Block = _block,
        };
    }

    private ZplOp MakeBarcode(BarcodeSpec s, string data)
    {
        var ec = s.Ec;
        if (s.Symbology == ZplSymbology.QrCode)
        {
            // ZPL QR field data starts with "<error correction><mode>," (for example "QA,") before the real data.
            var m = QrPrefix().Match(data);
            if (m.Success)
            {
                if (m.Groups["ec"].Success) ec = m.Groups["ec"].Value[0];
                data = data[m.Length..];
            }
        }
        // Codabar: the data is framed by the command's start and stop letters (A to D) before it is drawn.
        if (s.Symbology == ZplSymbology.Codabar && !ZXingBarcodeEncoder.SplitCodabar(data).Framed)
            data = char.ToUpperInvariant(s.Start) + data + char.ToUpperInvariant(s.Stop);
        return new BarcodeOp
        {
            X = _x, Y = _y, Baseline = _baseline, Reverse = _reverse, Symbology = s.Symbology, Data = data,
            HeightDots = s.Height, ModuleWidth = s.ModuleSize, ShowText = s.ShowText, TextAbove = s.TextAbove,
            Orientation = s.Orientation, QrErrorCorrection = ec, WideToNarrowRatio = _wideRatio,
            Pdf417Security = s.Security, Pdf417Columns = s.Columns,
        };
    }

    /// <summary>Decodes <c>_XX</c> hex escapes (the character set by <c>^FH</c>) as UTF-8 bytes.</summary>
    private static string DecodeHex(string text, char escape)
    {
        var result = new StringBuilder();
        var bytes = new List<byte>();
        void Flush()
        {
            if (bytes.Count == 0) return;
            result.Append(Encoding.UTF8.GetString(bytes.ToArray()));
            bytes.Clear();
        }
        for (var i = 0; i < text.Length; i++)
        {
            // An escape needs two hex digits after it, so the last valid position for the escape is Length - 3.
            if (text[i] == escape && i + 2 < text.Length
                && Uri.IsHexDigit(text[i + 1]) && Uri.IsHexDigit(text[i + 2]))
            {
                bytes.Add(byte.Parse(text.AsSpan(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                i += 2;
            }
            else
            {
                Flush();
                result.Append(text[i]);
            }
        }
        Flush();
        return result.ToString();
    }

    private void Warn(string key, string message)
    {
        if (_warned.Add(key)) _doc.Warnings.Add(message);
    }

    private static int Int(string[] p, int i, int fallback) =>
        i < p.Length && int.TryParse(p[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static double Dbl(string[] p, int i, double fallback) =>
        i < p.Length && double.TryParse(p[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static char Chr(string[] p, int i, char fallback)
    {
        if (i >= p.Length) return fallback;
        var s = p[i].Trim();
        return s.Length == 0 ? fallback : char.ToUpperInvariant(s[0]);
    }
}
