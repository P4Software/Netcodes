using System.Text;

namespace Netcodes.Zpl;

/// <summary>Protects field data so it cannot be mistaken for ZPL commands.</summary>
internal static class ZplEscape
{
    /// <summary>
    /// Escapes <c>^</c>, <c>~</c> and <c>_</c> as <c>_XX</c> hex (used together with the <c>^FH_</c> command) and drops
    /// control characters. Why: ZPL has no quoting, so one stray caret in a customer name would start a command.
    /// Non-ASCII text is left alone because the generator selects UTF-8 with <c>^CI28</c>.
    /// </summary>
    /// <param name="text">The raw field text.</param>
    /// <returns>The escaped text and whether <c>^FH_</c> must precede the field.</returns>
    public static (string Escaped, bool NeedsHex) Escape(string text)
    {
        var sb = new StringBuilder(text.Length);
        var needsHex = false;
        foreach (var c in text)
        {
            if (c is '^' or '~' or '_')
            {
                sb.Append('_').Append(((int)c).ToString("X2"));
                needsHex = true;
            }
            else if (c >= ' ')
            {
                sb.Append(c);
            }
        }
        return (sb.ToString(), needsHex);
    }
}
