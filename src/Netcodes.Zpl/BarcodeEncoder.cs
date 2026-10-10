using System.Text.RegularExpressions;
using ZXing;
using ZXing.Common;
using ZXing.QrCode.Internal;

namespace Netcodes.Zpl;

/// <summary>Raised when barcode data cannot be drawn. The message says what is wrong with the data.</summary>
public sealed class BarcodeException : Exception
{
    /// <summary>Creates the exception with a plain-language message.</summary>
    /// <param name="message">What is wrong and how to fix it.</param>
    /// <param name="inner">The underlying error, for diagnostics only.</param>
    public BarcodeException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>The dark and light modules of an encoded barcode (one cell per module, no quiet zone).</summary>
public sealed class BarcodeMatrix
{
    private readonly bool[] _modules;

    /// <summary>Creates a matrix from row-major module data.</summary>
    /// <param name="width">Number of columns.</param>
    /// <param name="height">Number of rows (1 for linear barcodes).</param>
    /// <param name="modules">Row-major values; true is dark.</param>
    public BarcodeMatrix(int width, int height, bool[] modules)
    {
        Width = width;
        Height = height;
        _modules = modules;
    }

    /// <summary>Number of columns.</summary>
    public int Width { get; }
    /// <summary>Number of rows.</summary>
    public int Height { get; }
    /// <summary>True when the module at the given column and row is dark.</summary>
    public bool this[int x, int y] => _modules[y * Width + x];
}

/// <summary>Settings that change how a barcode's modules are laid out (from <c>^BY</c> and <c>^B7</c>).</summary>
/// <param name="WideToNarrowRatio">Width of a wide element compared with a narrow one, 2.0 to 3.0 (ZPL's default is 3).</param>
/// <param name="Pdf417Security">PDF417 security level, 0 to 8 (it is the error-correction level).</param>
/// <param name="Pdf417Columns">PDF417 data columns, 1 to 30; 0 chooses automatically.</param>
public sealed record BarcodeOptions(double WideToNarrowRatio = 3.0, int Pdf417Security = 0, int Pdf417Columns = 0);

/// <summary>Turns barcode data into modules. Kept as an interface so the library behind it can be replaced.</summary>
public interface IBarcodeEncoder
{
    /// <summary>Encodes <paramref name="data"/> as the given symbology.</summary>
    /// <param name="symbology">The barcode type.</param>
    /// <param name="data">The data (for GS1-128 the bracketed text).</param>
    /// <param name="qrErrorCorrection">L, M, Q or H; only used for QR codes.</param>
    /// <param name="options">Element ratio and PDF417 settings; null uses the ZPL defaults.</param>
    /// <exception cref="BarcodeException">The data cannot be encoded as this symbology.</exception>
    BarcodeMatrix Encode(ZplSymbology symbology, string data, char qrErrorCorrection = 'Q', BarcodeOptions? options = null);
}

/// <summary>Encoder backed by ZXing.Net (Apache-2.0).</summary>
public sealed partial class ZXingBarcodeEncoder : IBarcodeEncoder
{
    [GeneratedRegex(@"^[0-9A-Z\-\. \$/\+%]+$")]
    private static partial Regex Code39Chars();

    [GeneratedRegex(@"^[0-9]+$")]
    private static partial Regex Digits();

    [GeneratedRegex(@"^[0-9\-\$:/\.\+]+$")]
    private static partial Regex CodabarChars();

    /// <inheritdoc />
    public BarcodeMatrix Encode(ZplSymbology symbology, string data, char qrErrorCorrection = 'Q', BarcodeOptions? options = null)
    {
        if (string.IsNullOrEmpty(data))
            throw new BarcodeException("A barcode has no data, so it was not drawn.");

        var (format, contents) = Prepare(symbology, data);
        options ??= new BarcodeOptions();
        // MARGIN 0 and a requested size of 0 make ZXing return exactly one cell per module; the renderer
        // then scales by the ZPL module width itself, which is how a printer lays out dots.
        var hints = new Dictionary<EncodeHintType, object> { [EncodeHintType.MARGIN] = 0 };
        if (symbology == ZplSymbology.QrCode)
        {
            hints[EncodeHintType.ERROR_CORRECTION] = qrErrorCorrection switch
            {
                'L' => ErrorCorrectionLevel.L,
                'M' => ErrorCorrectionLevel.M,
                'H' => ErrorCorrectionLevel.H,
                _ => ErrorCorrectionLevel.Q,
            };
            hints[EncodeHintType.CHARACTER_SET] = "UTF-8";
        }

        try
        {
            if (symbology == ZplSymbology.Pdf417) return EncodePdf417(contents, options);
            var matrix = ToMatrix(new MultiFormatWriter().encode(contents, format, 0, 0, hints));
            // Two-width codes: ZXing draws a wide element two modules wide, ZPL's default ratio makes it three.
            return symbology is ZplSymbology.Code39 or ZplSymbology.Codabar or ZplSymbology.Msi
                ? WidenWideElements(matrix, options.WideToNarrowRatio)
                : matrix;
        }
        catch (Exception ex) when (ex is not BarcodeException)
        {
            throw new BarcodeException(
                $"The data \"{Shorten(data)}\" cannot be drawn as {Name(symbology)}: {ex.Message}", ex);
        }
    }

    private static BarcodeMatrix ToMatrix(ZXing.Common.BitMatrix bits)
    {
        var modules = new bool[bits.Width * bits.Height];
        for (var y = 0; y < bits.Height; y++)
            for (var x = 0; x < bits.Width; x++)
                modules[y * bits.Width + x] = bits[x, y];
        return new BarcodeMatrix(bits.Width, bits.Height, modules);
    }

    // Longest symbol (in rows) before another column of data is added. Measured from Labelary (19, 19 and 17 rows for
    // its 1, 3 and 3 column samples); ZXing needs one codeword more than Labelary for the 26-character sample (20 rows
    // in one column), and any limit from 19 to 24 reproduces all four measured samples, so 20 is used.
    private const int Pdf417PreferredMaxRows = 20;

    private static BarcodeMatrix EncodePdf417(string contents, BarcodeOptions options)
    {
        // The ZPL security level is the PDF417 error-correction level (0 to 8) as it stands.
        var security = Math.Clamp(options.Pdf417Security, 0, 8);
        if (options.Pdf417Columns > 0)
            return Pdf417WithColumns(contents, security, Math.Clamp(options.Pdf417Columns, 1, 30));
        // No column count given: use the fewest columns that keep the symbol within the preferred number of rows.
        for (var columns = 1; columns <= 30; columns++)
        {
            try
            {
                var matrix = Pdf417WithColumns(contents, security, columns);
                if (matrix.Height <= Pdf417PreferredMaxRows || columns == 30) return matrix;
            }
            catch (Exception) when (columns < 30)
            {
                // Too much data for this few columns; try one more.
            }
        }
        throw new BarcodeException("The data does not fit in a PDF417 barcode. Use less data.");
    }

    private static BarcodeMatrix Pdf417WithColumns(string contents, int security, int columns)
    {
        var hints = new Dictionary<EncodeHintType, object>
        {
            [EncodeHintType.MARGIN] = 0,
            [EncodeHintType.ERROR_CORRECTION] = security,
            [EncodeHintType.PDF417_DIMENSIONS] = new ZXing.PDF417.Internal.Dimensions(columns, columns, 3, 90),
        };
        return CollapseRepeatedRows(ToMatrix(new MultiFormatWriter().encode(contents, BarcodeFormat.PDF_417, 0, 0, hints)));
    }

    // Every element of these codes alternates bar and space, so a run of two modules is exactly one wide element and
    // can be stretched to the ratio's width without touching a narrow one (which is one module, or the one-module gap
    // between characters).
    private static BarcodeMatrix WidenWideElements(BarcodeMatrix m, double ratio)
    {
        var wide = (int)Math.Round(Math.Clamp(ratio, 2.0, 3.0), MidpointRounding.AwayFromZero);
        if (wide <= 2) return m;
        var runs = new List<(bool Dark, int Length)>();
        for (var x = 0; x < m.Width;)
        {
            var dark = m[x, 0];
            var start = x;
            while (x < m.Width && m[x, 0] == dark) x++;
            runs.Add((dark, x - start));
        }
        var modules = new List<bool>();
        foreach (var (dark, length) in runs)
            for (var i = 0; i < (length == 2 ? wide : length); i++) modules.Add(dark);
        return new BarcodeMatrix(modules.Count, 1, modules.ToArray());
    }

    /// <summary>
    /// Splits Codabar data into its start letter, stop letter and body. Data that begins and ends with a letter from A to D
    /// is "framed"; otherwise the start and stop letters are A, which is what a printer uses when none is given.
    /// </summary>
    internal static (bool Framed, char Start, char Stop, string Body) SplitCodabar(string data)
    {
        var framed = data.Length >= 2 && "ABCD".Contains(char.ToUpperInvariant(data[0])) && "ABCD".Contains(char.ToUpperInvariant(data[^1]));
        return framed
            ? (true, char.ToUpperInvariant(data[0]), char.ToUpperInvariant(data[^1]), data[1..^1])
            : (false, 'A', 'A', data);
    }

    // ZXing draws every PDF417 row several times over so the symbol has a sensible shape. The ZPL row height is set
    // separately (^B7), so keep one row per logical row. Neighbouring logical rows always differ (each starts with a
    // different row indicator), so dropping repeats cannot remove a real row.
    private static BarcodeMatrix CollapseRepeatedRows(BarcodeMatrix m)
    {
        var rows = new List<bool[]>();
        for (var y = 0; y < m.Height; y++)
        {
            var row = new bool[m.Width];
            for (var x = 0; x < m.Width; x++) row[x] = m[x, y];
            if (rows.Count == 0 || !rows[^1].AsSpan().SequenceEqual(row)) rows.Add(row);
        }
        var modules = new bool[m.Width * rows.Count];
        for (var y = 0; y < rows.Count; y++) rows[y].CopyTo(modules, y * m.Width);
        return new BarcodeMatrix(m.Width, rows.Count, modules);
    }

    private static (BarcodeFormat Format, string Contents) Prepare(ZplSymbology symbology, string data)
    {
        switch (symbology)
        {
            case ZplSymbology.Code128:
                // Code 128 as printed here covers the standard ASCII set; letters like ü or ñ cannot be encoded.
                if (data.Any(c => c < ' ' || c > '~'))
                    throw new BarcodeException($"Code 128 can only contain standard keyboard characters (letters, digits and symbols such as - . /), but the data is \"{Shorten(data)}\".");
                return (BarcodeFormat.CODE_128, data);
            case ZplSymbology.Gs1128:
            {
                var encoded = Gs1.ToEncoderData(data);
                if (encoded.Length == 0)
                    throw new BarcodeException("GS1-128 data must be written as (identifier)value, for example (00)106141411234567897.");
                // A leading FNC1 marks the symbol as GS1-128.
                return (BarcodeFormat.CODE_128, 'ñ' + encoded);
            }
            case ZplSymbology.Code39:
                if (!Code39Chars().IsMatch(data))
                    throw new BarcodeException($"Code 39 can only contain digits, capital letters and - . space $ / + %, but the data is \"{data}\".");
                return (BarcodeFormat.CODE_39, data);
            case ZplSymbology.Ean13:
                if (!data.All(char.IsAsciiDigit) || data.Length is not (12 or 13))
                    throw new BarcodeException($"EAN-13 needs 12 or 13 digits, but the data is \"{Shorten(data)}\".");
                CheckDigitOrThrow("EAN-13", data, 13);
                return (BarcodeFormat.EAN_13, data);
            case ZplSymbology.UpcA:
                if (!data.All(char.IsAsciiDigit) || data.Length is not (11 or 12))
                    throw new BarcodeException($"UPC-A needs 11 or 12 digits, but the data is \"{Shorten(data)}\".");
                CheckDigitOrThrow("UPC-A", data, 12);
                return (BarcodeFormat.UPC_A, data);
            case ZplSymbology.Itf:
                if (!data.All(char.IsAsciiDigit))
                    throw new BarcodeException($"Interleaved 2 of 5 can only contain digits, but the data is \"{data}\".");
                // The symbology encodes digits in pairs, so an odd count gets a leading zero (as printers do).
                return (BarcodeFormat.ITF, data.Length % 2 == 1 ? "0" + data : data);
            case ZplSymbology.Code93:
                if (!Code39Chars().IsMatch(data))
                    throw new BarcodeException($"Code 93 can only contain digits, capital letters and - . space $ / + %, but the data is \"{Shorten(data)}\".");
                return (BarcodeFormat.CODE_93, data);
            case ZplSymbology.Codabar:
            {
                // Codabar frames its data with a start and a stop letter (A to D). Without them the printer uses A and A.
                var (_, start, stop, body) = SplitCodabar(data);
                if (body.Length == 0 || !CodabarChars().IsMatch(body))
                    throw new BarcodeException($"Codabar can only contain digits and - $ : / . + (optionally between a start and stop letter A to D), but the data is \"{Shorten(data)}\".");
                return (BarcodeFormat.CODABAR, start + body + stop);
            }
            case ZplSymbology.Msi:
                if (!Digits().IsMatch(data))
                    throw new BarcodeException($"MSI can only contain digits, but the data is \"{Shorten(data)}\".");
                return (BarcodeFormat.MSI, data);
            case ZplSymbology.Aztec:
                return (BarcodeFormat.AZTEC, data);
            case ZplSymbology.Pdf417:
                return (BarcodeFormat.PDF_417, data);
            case ZplSymbology.QrCode:
                return (BarcodeFormat.QR_CODE, data);
            case ZplSymbology.DataMatrix:
                return (BarcodeFormat.DATA_MATRIX, data);
            default:
                throw new BarcodeException($"The barcode type {symbology} is not supported yet.");
        }
    }

    /// <summary>
    /// Checks barcode data without drawing anything (cheap), so callers that only generate ZPL still learn about
    /// data a printer could not turn into a readable barcode.
    /// </summary>
    /// <param name="symbology">The barcode type.</param>
    /// <param name="data">The data to check.</param>
    /// <returns>A plain-language problem description, or <c>null</c> when the data is usable.</returns>
    public static string? Check(ZplSymbology symbology, string data)
    {
        if (string.IsNullOrEmpty(data)) return "A barcode has no data.";
        try
        {
            Prepare(symbology, data);
            return null;
        }
        catch (BarcodeException ex)
        {
            return ex.Message;
        }
    }

    // Messages are shown to people, so long data is shortened rather than quoted in full.
    private static string Shorten(string data) => data.Length <= 40 ? data : data[..40] + "...";

    // EAN-13 and UPC-A carry their own check digit; a wrong one makes scanners reject the printed code.
    private static void CheckDigitOrThrow(string name, string data, int fullLength)
    {
        if (data.Length != fullLength) return;
        var expected = Gs1.CheckDigit(data[..^1]);
        if (data[^1] - '0' != expected)
            throw new BarcodeException($"The {name} number \"{data}\" has a wrong check digit; the last digit should be {expected}.");
    }

    private static string Name(ZplSymbology s) => s switch
    {
        ZplSymbology.Code128 => "Code 128",
        ZplSymbology.Gs1128 => "GS1-128",
        ZplSymbology.Code39 => "Code 39",
        ZplSymbology.Ean13 => "EAN-13",
        ZplSymbology.UpcA => "UPC-A",
        ZplSymbology.Itf => "Interleaved 2 of 5",
        ZplSymbology.QrCode => "a QR code",
        ZplSymbology.DataMatrix => "a Data Matrix",
        ZplSymbology.Code93 => "Code 93",
        ZplSymbology.Codabar => "Codabar",
        ZplSymbology.Msi => "MSI",
        ZplSymbology.Aztec => "an Aztec code",
        ZplSymbology.Pdf417 => "PDF417",
        _ => s.ToString(),
    };
}
