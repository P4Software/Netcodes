using System.Text;
using System.Text.RegularExpressions;
using Netcodes.Template;

namespace Netcodes.Zpl;

/// <summary>GS1 helpers: check digits, application-identifier strings and the data a barcode encoder needs.</summary>
public static partial class Gs1
{
    private sealed record AiInfo(string Ai, int? Length);

    // Friendly template names for the identifiers warehouse labels use most. Length null = variable length.
    private static readonly Dictionary<string, AiInfo> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sscc"] = new("00", 18),
        ["gtin"] = new("01", 14),
        ["productionDate"] = new("11", 6),
        ["bestBefore"] = new("15", 6),
        ["expiry"] = new("17", 6),
        ["lot"] = new("10", null),
        ["serial"] = new("21", null),
        ["count"] = new("37", null),
    };

    [GeneratedRegex(@"\((?<ai>\d{2,4})\)(?<value>[^(]*)")]
    private static partial Regex Element();

    /// <summary>Computes the GS1 mod-10 check digit for a number that does not yet contain one.</summary>
    /// <param name="digits">The digits without the check digit.</param>
    public static int CheckDigit(string digits)
    {
        var sum = 0;
        // Weights alternate 3,1,3,1... starting from the rightmost digit.
        for (int i = digits.Length - 1, weight = 3; i >= 0; i--, weight = 4 - weight)
            sum += (digits[i] - '0') * weight;
        return (10 - sum % 10) % 10;
    }

    /// <summary>
    /// Builds the human-readable GS1 string, for example <c>(00)106141411234567897(10)LOT1</c>.
    /// Fixed-length identifiers come first, then variable-length ones, because a variable-length value must be
    /// followed by an FNC1 separator unless it is last.
    /// </summary>
    /// <param name="fields">Named identifier values (sscc, gtin, lot, expiry, bestBefore, productionDate, serial, count).</param>
    /// <param name="warnings">Receives notes such as a wrong check digit or an unknown field name.</param>
    /// <exception cref="TemplateException">A value has an impossible length or characters, so the label must not print.</exception>
    public static string Build(IReadOnlyDictionary<string, string> fields, ICollection<string> warnings)
    {
        var items = new List<(AiInfo Info, string Value)>();
        foreach (var (name, raw) in fields)
        {
            if (!Named.TryGetValue(name, out var info))
            {
                warnings.Add($"The GS1 field \"{name}\" is not known and was skipped. Known fields: {string.Join(", ", Named.Keys)}.");
                continue;
            }
            var value = (raw ?? "").Trim();
            if (value.Contains('(') || value.Contains(')'))
                throw new TemplateException($"The GS1 field \"{name}\" must not contain brackets. Remove them from \"{value}\".");
            value = info.Ai switch
            {
                "00" => WithCheckDigit("sscc", value, 18, warnings),
                "01" => WithCheckDigit("gtin", value, 14, warnings),
                _ => value,
            };
            if (info.Length is { } length && value.Length != length)
                throw new TemplateException($"The GS1 field \"{name}\" must have {length} characters, but \"{value}\" has {value.Length}.");
            if (value.Length == 0)
                throw new TemplateException($"The GS1 field \"{name}\" is empty. Give it a value or remove it from the template.");
            items.Add((info, value));
        }
        if (items.Count == 0)
            throw new TemplateException("The GS1 barcode has no fields. Add at least one, for example sscc.");

        var ordered = items.Where(i => i.Info.Length is not null).OrderBy(i => i.Info.Ai, StringComparer.Ordinal)
            .Concat(items.Where(i => i.Info.Length is null).OrderBy(i => i.Info.Ai, StringComparer.Ordinal));
        var sb = new StringBuilder();
        foreach (var (info, value) in ordered)
            sb.Append('(').Append(info.Ai).Append(')').Append(value);
        return sb.ToString();
    }

    /// <summary>
    /// Converts <c>(AI)value(AI)value</c> to the character string a Code 128 encoder needs: identifiers and values
    /// back to back, with U+00F1 (ZXing's FNC1 marker) after each variable-length value that is not the last.
    /// </summary>
    /// <param name="humanReadable">The bracketed GS1 string. Returns an empty string when it has no identifiers.</param>
    public static string ToEncoderData(string humanReadable)
    {
        var matches = Element().Matches(humanReadable);
        var sb = new StringBuilder();
        for (var i = 0; i < matches.Count; i++)
        {
            var ai = matches[i].Groups["ai"].Value;
            sb.Append(ai).Append(matches[i].Groups["value"].Value.Replace(" ", ""));
            if (i < matches.Count - 1 && !IsFixedLength(ai)) sb.Append('ñ');
        }
        return sb.ToString();
    }

    /// <summary>True when the identifier's value always has the same length, so no FNC1 separator follows it.</summary>
    /// <param name="ai">The application identifier digits.</param>
    public static bool IsFixedLength(string ai) =>
        ai.Length >= 2 && ai[..2] is "00" or "01" or "02" or "11" or "12" or "13" or "14" or "15" or "16" or "17"
            or "20" or "31" or "32" or "33" or "34" or "35" or "36" or "41";

    private static string WithCheckDigit(string field, string value, int fullLength, ICollection<string> warnings)
    {
        if (value.Length == 0 || !value.All(char.IsAsciiDigit))
            throw new TemplateException($"The {field} \"{value}\" must contain digits only and cannot be empty.");
        if (value.Length == fullLength - 1) return value + CheckDigit(value);
        if (value.Length != fullLength)
            throw new TemplateException(
                $"The {field} \"{value}\" must have {fullLength - 1} digits (the check digit is added for you) or {fullLength} digits.");
        var expected = CheckDigit(value[..^1]);
        if (value[^1] - '0' != expected)
            warnings.Add($"The {field} {value} has a wrong check digit; the last digit should be {expected}.");
        return value;
    }
}
