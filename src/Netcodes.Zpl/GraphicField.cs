using System.Text;

namespace Netcodes.Zpl;

/// <summary>Reads and writes the ASCII-hex body of a <c>^GF</c> graphic, including Zebra's run-length shorthand.</summary>
internal static class GraphicField
{
    /// <summary>
    /// Largest graphic read or written, in bytes (16 million dots). Why: the size in the ZPL is untrusted, and the
    /// shorthand lets a few characters stand for millions of nibbles, so the real output size is what is capped.
    /// </summary>
    public const int MaxBytes = 2_000_000;

    /// <summary>
    /// Most graphic data one label may hold in total, in bytes. Why: <see cref="MaxBytes"/> limits one graphic, but a label
    /// can have thousands of fields, and a field of 30 characters can declare a 2 MB graphic.
    /// </summary>
    public const int MaxTotalBytes = 8_000_000;

    /// <summary>Widest row accepted: 4000 dots, the widest label the preview draws.</summary>
    public const int MaxBytesPerRow = 500;

    /// <summary>
    /// Decodes the data part of <c>^GFA</c>. Returns null (with a plain-language <paramref name="problem"/>) when the
    /// graphic cannot be used. Rows that the data does not supply are blank.
    /// </summary>
    /// <param name="data">The hex text, possibly with count letters, <c>,</c> <c>!</c> and <c>:</c>.</param>
    /// <param name="bytesPerRow">Bytes in one row.</param>
    /// <param name="declaredTotal">Total bytes the field claims; it fixes the number of rows (and is capped).</param>
    /// <param name="problem">Why the graphic was rejected.</param>
    public static byte[]? Decode(string data, int bytesPerRow, int declaredTotal, out string? problem)
    {
        problem = null;
        if (bytesPerRow < 1 || bytesPerRow > MaxBytesPerRow)
        {
            problem = $"A graphic has {bytesPerRow} bytes per row; it must be between 1 and {MaxBytesPerRow}. The graphic was left out.";
            return null;
        }
        if (declaredTotal > MaxBytes)
        {
            problem = $"A graphic declares more data than the preview allows ({declaredTotal:N0} bytes; the most is {MaxBytes:N0}). The graphic was left out.";
            return null;
        }
        if (declaredTotal < bytesPerRow)
        {
            problem = "A graphic declares less data than one row. The graphic was left out.";
            return null;
        }

        // The declared total fixes the number of rows; decoding stops there, so nothing a flood of repeat letters says
        // can make the output larger than that (and that is capped above).
        var maxRows = declaredTotal / bytesPerRow;
        var nibblesPerRow = bytesPerRow * 2;
        var rows = new List<byte[]>();
        var row = new byte[nibblesPerRow];
        var column = 0;
        var count = 0;
        byte[]? previous = null;

        void EndRow()
        {
            rows.Add(row);
            previous = row;
            row = new byte[nibblesPerRow];
            column = 0;
        }

        foreach (var c in data)
        {
            if (rows.Count >= maxRows) break;
            if (char.IsWhiteSpace(c)) continue;
            if (c is >= 'G' and <= 'Y') { count += c - 'G' + 1; continue; }
            if (c is >= 'g' and <= 'z') { count += (c - 'g' + 1) * 20; continue; }
            if (c == ',') { EndRow(); count = 0; continue; }                       // rest of the row is zero
            if (c == '!') { Array.Fill(row, (byte)15, column, nibblesPerRow - column); EndRow(); count = 0; continue; }
            if (c == ':')
            {
                // Same as the row above; only meaningful at the start of a row.
                if (previous is not null && column == 0) { row = (byte[])previous.Clone(); EndRow(); }
                count = 0;
                continue;
            }
            if (!Uri.IsHexDigit(c)) { count = 0; continue; }
            var nibble = (byte)Convert.ToInt32(c.ToString(), 16);
            for (var i = Math.Max(1, count); i > 0 && rows.Count < maxRows; i--)
            {
                row[column++] = nibble;
                if (column == nibblesPerRow) EndRow();
            }
            count = 0;
        }
        if (column > 0 && rows.Count < maxRows) EndRow();       // a half-written last row is kept, padded with zeros
        if (rows.Count == 0)
        {
            problem = "A graphic has no picture data. The graphic was left out.";
            return null;
        }
        // Rows the data did not supply stay blank, so the graphic has the height the field declared.
        var bytes = new byte[maxRows * bytesPerRow];
        for (var y = 0; y < rows.Count; y++)
            for (var x = 0; x < bytesPerRow; x++)
                bytes[y * bytesPerRow + x] = (byte)((rows[y][x * 2] << 4) | rows[y][x * 2 + 1]);
        return bytes;
    }

    /// <summary>
    /// Writes packed rows as ASCII hex with Zebra's run-length shorthand: a letter prefix is a repeat count (G to Y = 1 to
    /// 19, g to z = 20 to 400), <c>,</c> fills the rest of a row with zeros, <c>!</c> with ones and <c>:</c> repeats the
    /// row above. Logos are mostly long runs, so this keeps the ZPL (and the printer transfer) small.
    /// </summary>
    /// <param name="bits">The packed rows, most significant bit first.</param>
    /// <param name="bytesPerRow">Bytes in one row.</param>
    public static string Encode(byte[] bits, int bytesPerRow)
    {
        var sb = new StringBuilder();
        string? previous = null;
        for (var offset = 0; offset < bits.Length; offset += bytesPerRow)
        {
            var hex = Convert.ToHexString(bits, offset, bytesPerRow);
            if (hex == previous) { sb.Append(':'); continue; }
            previous = hex;
            // A trailing run of zeros or ones becomes , or ! (the rest of the row).
            var tail = hex[^1];
            if (tail is '0' or 'F')
            {
                var start = hex.Length;
                while (start > 0 && hex[start - 1] == tail) start--;
                AppendRuns(sb, hex[..start]);
                sb.Append(tail == '0' ? ',' : '!');
            }
            else AppendRuns(sb, hex);
        }
        return sb.ToString();
    }

    private static void AppendRuns(StringBuilder sb, string hex)
    {
        for (var i = 0; i < hex.Length;)
        {
            var run = 1;
            while (i + run < hex.Length && hex[i + run] == hex[i]) run++;
            var n = run;
            // One letter can say at most 400, so longer runs take several letters in a row (they add up).
            while (n >= 20) { var k = Math.Min(n / 20, 20); sb.Append((char)('g' + k - 1)); n -= k * 20; }
            if (n > 1) sb.Append((char)('G' + n - 1));
            sb.Append(hex[i]);
            i += run;
        }
    }
}
