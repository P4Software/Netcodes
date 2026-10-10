namespace Netcodes.Zpl;

/// <summary>
/// What one label may spend on encoding barcodes. Why: each barcode is capped (8000 characters), but a label can hold
/// thousands of them, and a PDF417 is encoded several times while its column count is chosen, so the cost of a label
/// is the sum over all of its barcodes. The budget is shared by every barcode of one label.
/// </summary>
internal sealed class BarcodeWorkBudget
{
    // About 200 labels' worth of ordinary barcodes, or 20 PDF417 of 1000 characters.
    private const long Total = 200_000;
    // A PDF417 is encoded up to 30 times (once per candidate column count), so it counts ten times over.
    private const int Pdf417Weight = 10;

    private long _remaining = Total;
    private bool _warned;

    /// <summary>Takes the cost of one barcode; false when the label has used up its budget.</summary>
    /// <param name="symbology">The barcode type.</param>
    /// <param name="dataLength">Length of the barcode's data.</param>
    public bool TryTake(ZplSymbology symbology, int dataLength)
    {
        var cost = (symbology == ZplSymbology.Pdf417 ? Pdf417Weight : 1L) * Math.Max(1, dataLength);
        if (cost > _remaining) return false;
        _remaining -= cost;
        return true;
    }

    /// <summary>The plain-language warning, or null when it was already given (one per label is enough).</summary>
    public string? WarningOnce()
    {
        if (_warned) return null;
        _warned = true;
        return "The label has too much barcode data in total, so some barcodes were left out. Use fewer or smaller barcodes.";
    }
}
