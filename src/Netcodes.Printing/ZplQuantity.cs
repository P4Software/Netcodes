using System.Text.RegularExpressions;

namespace Netcodes.Printing;

/// <summary>Applies a number of copies to a ZPL label using the <c>^PQ</c> command.</summary>
public static partial class ZplQuantity
{
    [GeneratedRegex(@"\^PQ[^\^~]*", RegexOptions.IgnoreCase)]
    private static partial Regex ExistingQuantity();

    /// <summary>
    /// Returns ZPL that prints <paramref name="copies"/> labels. One copy leaves the ZPL untouched; otherwise any
    /// existing <c>^PQ</c> is replaced and a new one is placed before the closing <c>^XZ</c>.
    /// </summary>
    /// <param name="zpl">The label.</param>
    /// <param name="copies">Number of copies, 1 or more.</param>
    public static string Apply(string zpl, int copies)
    {
        if (copies <= 1) return zpl;
        var cleaned = ExistingQuantity().Replace(zpl, "");
        var end = cleaned.LastIndexOf("^XZ", StringComparison.OrdinalIgnoreCase);
        var quantity = "^PQ" + copies;
        return end < 0 ? cleaned + quantity : cleaned.Insert(end, quantity);
    }
}
