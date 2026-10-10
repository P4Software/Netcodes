namespace Netcodes.Zpl;

/// <summary>Barcode types the parser and renderer understand.</summary>
public enum ZplSymbology
{
    /// <summary>Code 128.</summary>
    Code128,
    /// <summary>GS1-128 (Code 128 in ZPL mode D).</summary>
    Gs1128,
    /// <summary>Code 39.</summary>
    Code39,
    /// <summary>EAN-13.</summary>
    Ean13,
    /// <summary>UPC-A.</summary>
    UpcA,
    /// <summary>Interleaved 2 of 5.</summary>
    Itf,
    /// <summary>QR code.</summary>
    QrCode,
    /// <summary>Data Matrix.</summary>
    DataMatrix,
    /// <summary>Code 93.</summary>
    Code93,
    /// <summary>Codabar.</summary>
    Codabar,
    /// <summary>MSI (Plessey).</summary>
    Msi,
    /// <summary>Aztec Code.</summary>
    Aztec,
    /// <summary>PDF417 (stacked two-dimensional barcode).</summary>
    Pdf417,
}

/// <summary>A text block set up with <c>^FB</c>.</summary>
/// <param name="WidthDots">Width the text wraps inside.</param>
/// <param name="MaxLines">Maximum number of lines.</param>
/// <param name="LineSpacing">Extra dots between lines.</param>
/// <param name="Justify">L, C, R or J.</param>
public sealed record ZplBlock(int WidthDots, int MaxLines, int LineSpacing, char Justify);

/// <summary>One thing to draw. Positions are printer dots from the label's top-left corner.</summary>
public abstract record ZplOp
{
    /// <summary>Horizontal position in dots.</summary>
    public int X { get; init; }
    /// <summary>Vertical position in dots.</summary>
    public int Y { get; init; }
    /// <summary>True when the position came from <c>^FT</c> (bottom-left origin) rather than <c>^FO</c>.</summary>
    public bool Baseline { get; init; }
    /// <summary>True when the field is printed in reverse (<c>^FR</c>).</summary>
    public bool Reverse { get; init; }
}

/// <summary>A text field.</summary>
public sealed record TextOp : ZplOp
{
    /// <summary>The text to draw.</summary>
    public string Text { get; init; } = "";
    /// <summary>ZPL font letter or digit.</summary>
    public string Font { get; init; } = "0";
    /// <summary>Character height in dots.</summary>
    public int HeightDots { get; init; }
    /// <summary>Character width in dots (0 = proportional).</summary>
    public int WidthDots { get; init; }
    /// <summary>N, R, I or B.</summary>
    public char Orientation { get; init; } = 'N';
    /// <summary>Wrapping block, if the field had <c>^FB</c>.</summary>
    public ZplBlock? Block { get; init; }
}

/// <summary>A rectangle or line from <c>^GB</c>.</summary>
public sealed record BoxOp : ZplOp
{
    /// <summary>Width in dots.</summary>
    public int WidthDots { get; init; }
    /// <summary>Height in dots.</summary>
    public int HeightDots { get; init; }
    /// <summary>Border thickness in dots.</summary>
    public int Thickness { get; init; } = 1;
    /// <summary>Corner rounding (parsed, not drawn yet).</summary>
    public int Rounding { get; init; }
    /// <summary>True for a black line, false for a white one.</summary>
    public bool Black { get; init; } = true;
}

/// <summary>A circle from <c>^GC</c>.</summary>
public sealed record CircleOp : ZplOp
{
    /// <summary>Diameter in dots.</summary>
    public int Diameter { get; init; }
    /// <summary>Border thickness in dots.</summary>
    public int Thickness { get; init; } = 1;
    /// <summary>True for a black line, false for a white one.</summary>
    public bool Black { get; init; } = true;
}

/// <summary>A barcode field.</summary>
public sealed record BarcodeOp : ZplOp
{
    /// <summary>The barcode type.</summary>
    public ZplSymbology Symbology { get; init; }
    /// <summary>The field data (for GS1-128 the bracketed text).</summary>
    public string Data { get; init; } = "";
    /// <summary>Bar height in dots (1D codes).</summary>
    public int HeightDots { get; init; }
    /// <summary>Narrow bar width, or the size of one module for QR and Data Matrix, in dots.</summary>
    public int ModuleWidth { get; init; } = 2;
    /// <summary>Whether the human-readable line is printed.</summary>
    public bool ShowText { get; init; } = true;
    /// <summary>Whether the human-readable line is above the bars.</summary>
    public bool TextAbove { get; init; }
    /// <summary>N, R, I or B.</summary>
    public char Orientation { get; init; } = 'N';
    /// <summary>QR error correction: L, M, Q or H.</summary>
    public char QrErrorCorrection { get; init; } = 'Q';
    /// <summary>Wide element width compared with a narrow one, from <c>^BY</c> (default 3).</summary>
    public double WideToNarrowRatio { get; init; } = 3.0;
    /// <summary>PDF417 security level, 0 to 8.</summary>
    public int Pdf417Security { get; init; }
    /// <summary>PDF417 data columns; 0 means automatic.</summary>
    public int Pdf417Columns { get; init; }
}

/// <summary>A one-bit graphic from <c>^GF</c> (a picture turned into black and white dots).</summary>
public sealed record ImageOp : ZplOp
{
    /// <summary>Width in dots (8 per byte of a row).</summary>
    public int WidthDots => BytesPerRow * 8;
    /// <summary>Height in dots (number of rows).</summary>
    public int HeightDots => BytesPerRow == 0 ? 0 : Bits.Length / BytesPerRow;
    /// <summary>Bytes in one row.</summary>
    public int BytesPerRow { get; init; }
    /// <summary>The rows, most significant bit first; a set bit is a black dot.</summary>
    public byte[] Bits { get; init; } = Array.Empty<byte>();
}

/// <summary>The result of parsing a ZPL label.</summary>
public sealed class ZplDocument
{
    /// <summary>Label width from <c>^PW</c>, if present.</summary>
    public int? WidthDots { get; set; }
    /// <summary>Label length from <c>^LL</c>, if present.</summary>
    public int? HeightDots { get; set; }
    /// <summary>Everything to draw, in order.</summary>
    public List<ZplOp> Ops { get; } = new();
    /// <summary>Plain-language notes about commands that were skipped.</summary>
    public List<string> Warnings { get; } = new();
}
