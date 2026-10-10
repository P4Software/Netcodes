using System.Text.Json.Serialization;

namespace Netcodes.Template;

/// <summary>Barcode symbologies a <see cref="BarcodeElement"/> can use.</summary>
public enum Symbology
{
    /// <summary>Code 128.</summary>
    Code128,
    /// <summary>Code 39.</summary>
    Code39,
    /// <summary>EAN-13.</summary>
    Ean13,
    /// <summary>UPC-A.</summary>
    UpcA,
    /// <summary>Interleaved 2 of 5.</summary>
    Itf,
    /// <summary>Data Matrix.</summary>
    DataMatrix,
    /// <summary>Code 93.</summary>
    Code93,
    /// <summary>Codabar.</summary>
    Codabar,
    /// <summary>MSI (Plessey).</summary>
    Msi,
    /// <summary>Aztec Code (two-dimensional).</summary>
    Aztec,
    /// <summary>PDF417 (stacked two-dimensional barcode used on IDs and tickets).</summary>
    Pdf417,
}

/// <summary>
/// Base class of everything placed on a label. Positions are millimetres from the label's
/// top-left corner (inside a <see cref="RepeatElement"/>, from the top-left of the current row).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextElement), "text")]
[JsonDerivedType(typeof(BarcodeElement), "barcode")]
[JsonDerivedType(typeof(QrElement), "qr")]
[JsonDerivedType(typeof(Gs1Element), "gs1")]
[JsonDerivedType(typeof(BoxElement), "box")]
[JsonDerivedType(typeof(RepeatElement), "repeat")]
[JsonDerivedType(typeof(ImageElement), "image")]
public abstract class LabelElement
{
    /// <summary>Distance from the left edge in millimetres.</summary>
    public double X { get; set; }
    /// <summary>Distance from the top edge in millimetres.</summary>
    public double Y { get; set; }
    /// <summary>Rotation in degrees: 0, 90, 180 or 270.</summary>
    public int Rotation { get; set; }
}

/// <summary>A line of text, optionally wrapped inside a maximum width.</summary>
public sealed class TextElement : LabelElement
{
    /// <summary>The text; may contain <c>{{placeholders}}</c>.</summary>
    public string Text { get => _text; set => _text = value ?? ""; }
    private string _text = "";
    /// <summary>The ZPL font letter or digit (default "0", the scalable font).</summary>
    public string Font { get => _font; set => _font = value ?? "0"; }
    private string _font = "0";
    /// <summary>Character height in millimetres.</summary>
    public double HeightMm { get; set; } = 4;
    /// <summary>Character width in millimetres; when null the width follows the height.</summary>
    public double? WidthMm { get; set; }
    /// <summary>When set, text wraps inside this width in millimetres.</summary>
    public double? MaxWidthMm { get; set; }
    /// <summary>Maximum number of wrapped lines (used with <see cref="MaxWidthMm"/>).</summary>
    public int MaxLines { get; set; } = 1;
}

/// <summary>A one- or two-dimensional barcode other than QR and GS1-128.</summary>
public sealed class BarcodeElement : LabelElement
{
    /// <summary>Which symbology to draw.</summary>
    public Symbology Symbology { get; set; } = Symbology.Code128;
    /// <summary>The data to encode; may contain <c>{{placeholders}}</c>.</summary>
    public string Data { get => _data; set => _data = value ?? ""; }
    private string _data = "";
    /// <summary>Bar height in millimetres.</summary>
    public double HeightMm { get; set; } = 12;
    /// <summary>Width of the narrowest bar in printer dots (1 to 10).</summary>
    public int ModuleWidth { get; set; } = 2;
    /// <summary>Whether to print the human-readable text under the bars.</summary>
    public bool ShowText { get; set; } = true;
}

/// <summary>A QR code.</summary>
public sealed class QrElement : LabelElement
{
    /// <summary>The data to encode; may contain <c>{{placeholders}}</c>.</summary>
    public string Data { get => _data; set => _data = value ?? ""; }
    private string _data = "";
    /// <summary>Magnification: dots per QR module (1 to 10).</summary>
    public int Size { get; set; } = 4;
    /// <summary>Error correction level: L, M, Q or H.</summary>
    public string ErrorCorrection { get => _errorCorrection; set => _errorCorrection = value ?? "Q"; }
    private string _errorCorrection = "Q";
}

/// <summary>
/// A GS1-128 barcode (for example an SSCC pallet label). Application identifiers are given as
/// named fields: sscc, gtin, lot, expiry, bestBefore, productionDate, serial, count.
/// </summary>
public sealed class Gs1Element : LabelElement
{
    /// <summary>Named application identifier values; values may contain <c>{{placeholders}}</c>.</summary>
    public Dictionary<string, string> Fields { get => _fields; set => _fields = value ?? new(); }
    private Dictionary<string, string> _fields = new();
    /// <summary>Bar height in millimetres.</summary>
    public double HeightMm { get; set; } = 15;
    /// <summary>Width of the narrowest bar in printer dots (1 to 10).</summary>
    public int ModuleWidth { get; set; } = 2;
}

/// <summary>
/// A picture (a logo, a photo) printed as black and white dots. The picture comes from the data, not from a file or
/// a web address: give it as base64 text or a <c>data:image/png;base64,...</c> address, usually through a placeholder
/// such as <c>{{logo}}</c>. PNG, JPEG, GIF, BMP and WebP are accepted.
/// </summary>
public sealed class ImageElement : LabelElement
{
    /// <summary>The picture as base64 or a data address; may contain <c>{{placeholders}}</c>.</summary>
    public string Data { get => _data; set => _data = value ?? ""; }
    private string _data = "";
    /// <summary>Printed width in millimetres.</summary>
    public double WidthMm { get; set; }
    /// <summary>Printed height in millimetres; when null the picture keeps its shape.</summary>
    public double? HeightMm { get; set; }
    /// <summary>Brightness below which a dot is black, 0 to 255 (default 128).</summary>
    public int Threshold { get; set; } = 128;
    /// <summary>When true black and white are swapped.</summary>
    public bool Invert { get; set; }
}

/// <summary>A rectangle outline or a filled block (a thin filled block is a line).</summary>
public sealed class BoxElement : LabelElement
{
    /// <summary>Width in millimetres.</summary>
    public double WidthMm { get; set; }
    /// <summary>Height in millimetres.</summary>
    public double HeightMm { get; set; }
    /// <summary>Border thickness in millimetres.</summary>
    public double BorderMm { get; set; } = 0.4;
    /// <summary>When true the box is solid black.</summary>
    public bool Filled { get; set; }
}

/// <summary>
/// Repeats its child elements once per item of a list in the data (packing-slip lines). Inside
/// the children, <c>{{item.field}}</c> reads the current item and <c>{{index}}</c> is its 1-based number.
/// </summary>
public sealed class RepeatElement : LabelElement
{
    /// <summary>Dotted path of the list in the data, for example <c>lines</c>.</summary>
    public string Source { get => _source; set => _source = value ?? ""; }
    private string _source = "";
    /// <summary>Vertical distance between rows in millimetres.</summary>
    public double RowHeightMm { get; set; }
    /// <summary>Upper limit of rows printed.</summary>
    public int MaxRows { get; set; } = 100;
    /// <summary>The elements drawn for each row, positioned relative to the row's top-left.</summary>
    public List<LabelElement> Elements { get => _elements; set => _elements = value ?? new(); }
    private List<LabelElement> _elements = new();
}
