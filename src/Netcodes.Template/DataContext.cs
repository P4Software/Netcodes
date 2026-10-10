using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Netcodes.Template;

/// <summary>
/// Resolves <c>{{dotted.path}}</c> placeholders against a JSON payload. A value that cannot be found
/// never aborts a label: it prints empty and a plain-language warning is recorded, because a half-filled
/// label is usually better noticed on the preview than lost at the printer.
/// </summary>
public sealed partial class DataContext
{
    [GeneratedRegex(@"\{\{\s*(?<path>[^{}\s]+)\s*\}\}")]
    private static partial Regex Placeholder();

    private readonly JsonElement _root;
    private readonly JsonElement? _item;
    private readonly int _index;

    /// <summary>Creates a context over the whole payload.</summary>
    /// <param name="root">The root JSON value (normally an object).</param>
    public DataContext(JsonElement root) => _root = root;

    private DataContext(JsonElement root, JsonElement item, int index)
    {
        _root = root;
        _item = item;
        _index = index;
    }

    /// <summary>Returns a context where <c>item.*</c> and <c>index</c> refer to one row of a list.</summary>
    /// <param name="item">The current list item.</param>
    /// <param name="index">Its zero-based position (exposed to templates as a 1-based <c>index</c>).</param>
    public DataContext ForItem(JsonElement item, int index) => new(_root, item, index);

    /// <summary>Replaces every placeholder in <paramref name="text"/> with its value.</summary>
    /// <param name="text">Text that may contain <c>{{placeholders}}</c>.</param>
    /// <param name="warnings">Receives a message for each value that is missing.</param>
    public string Expand(string text, ICollection<string> warnings)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("{{")) return text;
        return Placeholder().Replace(text, m => ValueOf(m.Groups["path"].Value, warnings));
    }

    /// <summary>Returns the items of the list at <paramref name="path"/>, or none (with a warning) if it is not a list.</summary>
    /// <param name="path">Dotted path of the list.</param>
    /// <param name="warnings">Receives a message when the list is missing.</param>
    public IReadOnlyList<JsonElement> GetArray(string path, ICollection<string> warnings)
    {
        if (TryResolve(path, out var value) && value.ValueKind == JsonValueKind.Array)
            return value.EnumerateArray().ToList();
        warnings.Add("The list \"" + path + "\" was not found in your data, or is not a list, so no rows were printed.");
        return Array.Empty<JsonElement>();
    }

    private string ValueOf(string path, ICollection<string> warnings)
    {
        if (path == "index" && _item is not null)
            return (_index + 1).ToString(CultureInfo.InvariantCulture);
        if (!TryResolve(path, out var v) || v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            warnings.Add("The field {{" + path + "}} has no value in your data. It will print empty.");
            return "";
        }
        switch (v.ValueKind)
        {
            case JsonValueKind.String: return v.GetString() ?? "";
            case JsonValueKind.Number: return v.GetRawText();
            case JsonValueKind.True: return "true";
            case JsonValueKind.False: return "false";
            default:
                warnings.Add("The field {{" + path + "}} holds a list or an object, not a single value. It will print empty.");
                return "";
        }
    }

    private bool TryResolve(string path, out JsonElement value)
    {
        value = default;
        var parts = path.Split('.');
        JsonElement current;
        var start = 0;
        // "item." only means something inside a repeat row; elsewhere it is an ordinary property name.
        if (_item is { } item && parts[0] == "item") { current = item; start = 1; }
        else current = _root;
        for (var i = start; i < parts.Length; i++)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(parts[i], out var next))
                return false;
            current = next;
        }
        value = current;
        return true;
    }
}
