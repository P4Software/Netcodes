using System.Text.Json;
using Netcodes.Template;

namespace Netcodes.Zpl;

/// <summary>The ZPL produced for a label and any warnings found while producing it.</summary>
/// <param name="Zpl">The ZPL text, ready to preview or send to a printer.</param>
/// <param name="Warnings">Plain-language notes, for example fields that had no value in the data.</param>
public sealed record ZplResult(string Zpl, IReadOnlyList<string> Warnings);

/// <summary>Generates ZPL from a template and data.</summary>
public static class ZplGenerator
{
    /// <summary>Generates ZPL from a template and a JSON data string (empty means no data).</summary>
    /// <param name="template">The label design.</param>
    /// <param name="dataJson">JSON object whose values fill the <c>{{placeholders}}</c>.</param>
    /// <param name="options">Optional printer settings written into the ZPL.</param>
    /// <exception cref="TemplateException">The template is invalid or the data is not valid JSON.</exception>
    public static ZplResult Generate(LabelTemplate template, string dataJson, GenerateOptions? options = null)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(dataJson) ? "{}" : dataJson);
        }
        catch (JsonException ex)
        {
            throw new TemplateException(
                "The data is not valid JSON (" + ex.Message + "). Check for a missing comma, quote or bracket.", ex);
        }
        using (doc)
            return Generate(template, doc.RootElement, options);
    }

    /// <summary>Generates ZPL from a template and an already-parsed JSON value.</summary>
    /// <param name="template">The label design.</param>
    /// <param name="data">The data that fills the <c>{{placeholders}}</c>.</param>
    /// <param name="options">Optional printer settings written into the ZPL.</param>
    /// <exception cref="TemplateException">The template or the printer settings are invalid.</exception>
    public static ZplResult Generate(LabelTemplate template, JsonElement data, GenerateOptions? options = null)
    {
        var problems = template.Validate().ToList();
        if (options?.PrintSpeed is < 1 or > 14)
            problems.Add("The print speed must be between 1 and 14.");
        if (options?.Darkness is < 0 or > 30)
            problems.Add("The print darkness must be between 0 and 30.");
        // A number that is not a real value (for example from JSON: "mode": 9) would index past the letter tables.
        if (options?.Media is { } media && !Enum.IsDefined(media))
            problems.Add("The media type is not one of the known types (thermal or direct).");
        if (options?.Mode is { } mode && !Enum.IsDefined(mode))
            problems.Add("The print mode is not one of the known modes.");
        if (options?.Tracking is { } tracking && !Enum.IsDefined(tracking))
            problems.Add("The media tracking is not one of the known settings.");
        if (options?.ConfigUpdate is { } update && !Enum.IsDefined(update))
            problems.Add("The configuration update must be save or recall.");
        if (options?.LabelShiftDots is < -9999 or > 9999)
            problems.Add("The label shift must be between -9999 and 9999 dots.");
        if (options?.Quantity is < 1 or > 10000)
            problems.Add("The quantity must be between 1 and 10000.");
        if (problems.Count > 0)
            throw new TemplateException(string.Join(" ", problems));

        var warnings = new List<string>();
        var writer = new ZplWriter(template, warnings);
        var context = new DataContext(data);
        writer.Header(template, options);
        foreach (var element in template.Elements)
            writer.Write(element, context, 0, 0);
        writer.Footer(options);
        return new ZplResult(writer.ToString(), warnings);
    }
}
