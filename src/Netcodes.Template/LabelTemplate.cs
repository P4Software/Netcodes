using System.Text.Json;
using System.Text.Json.Serialization;

namespace Netcodes.Template;

/// <summary>A label design: its size, resolution and the elements placed on it.</summary>
public sealed class LabelTemplate
{
    // Why AllowOutOfOrderMetadataProperties: hand-written JSON rarely puts "type" first.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        AllowOutOfOrderMetadataProperties = true,
    };

    /// <summary>The template's name, for example "shipping-label".</summary>
    public string Name { get => _name; set => _name = value ?? ""; }
    private string _name = "";
    /// <summary>Label width in millimetres.</summary>
    public double WidthMm { get; set; }
    /// <summary>Label height in millimetres.</summary>
    public double HeightMm { get; set; }
    /// <summary>Printer resolution in dots per inch: 152, 203, 300 or 600.</summary>
    public int Dpi { get; set; } = 203;
    /// <summary>The elements, drawn in order.</summary>
    public List<LabelElement> Elements { get => _elements; set => _elements = value ?? new(); }
    private List<LabelElement> _elements = new();

    /// <summary>Reads a template from JSON.</summary>
    /// <exception cref="TemplateException">The JSON is broken or describes an unknown element.</exception>
    public static LabelTemplate FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<LabelTemplate>(json, Options)
                ?? throw new TemplateException("The template is empty. Add a name, a label size and some elements.");
        }
        catch (JsonException ex)
        {
            throw new TemplateException(
                $"The template is not valid JSON ({ex.Message}). Check for a missing comma or quote near line {(ex.LineNumber ?? 0) + 1}, and that every element has a known type.", ex);
        }
        catch (NotSupportedException ex)
        {
            throw new TemplateException(
                "The template contains an element type that Netcodes does not know. Use text, barcode, qr, gs1, box, image or repeat.", ex);
        }
    }

    /// <summary>Writes the template as indented JSON.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>Checks the template and returns every problem found, in plain language (empty when valid).</summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(Name))
            problems.Add("The template has no name. Give it a name such as \"shipping-label\".");
        if (WidthMm <= 0 || HeightMm <= 0)
            problems.Add("The label size must be larger than zero. Set widthMm and heightMm in millimetres.");
        if (Dpi is not (152 or 203 or 300 or 600))
            problems.Add($"A resolution of {Dpi} dpi is not supported. Use 152, 203, 300 or 600.");
        ValidateElements(Elements, "", problems);
        return problems;
    }

    private static void ValidateElements(IEnumerable<LabelElement> elements, string where, List<string> problems)
    {
        var n = 0;
        foreach (var e in elements)
        {
            n++;
            // A JSON null inside the elements list deserializes to null; say so instead of crashing on it.
            if (e is null)
            {
                problems.Add($"The {where}element {n} is empty. Remove it or give it a type such as text or barcode.");
                continue;
            }
            var label =where + "element " + n + " (" + e.GetType().Name.Replace("Element", "").ToLowerInvariant() + ")";
            if (e.X < 0 || e.Y < 0)
                problems.Add($"The {label} is at a negative position. Use x and y of 0 or more.");
            if (e.Rotation is not (0 or 90 or 180 or 270))
                problems.Add($"The {label} has rotation {e.Rotation}. Use 0, 90, 180 or 270.");
            if (e is TextElement { HeightMm: <= 0 })
                problems.Add($"The {label} needs a heightMm larger than zero.");
            if (e is BarcodeElement { HeightMm: <= 0 } or Gs1Element { HeightMm: <= 0 })
                problems.Add($"The {label} needs a heightMm larger than zero.");
            if (e is BoxElement { WidthMm: <= 0 } or BoxElement { HeightMm: <= 0 })
                problems.Add($"The {label} needs widthMm and heightMm larger than zero.");
            if (e is BarcodeElement { } barcode && !Enum.IsDefined(barcode.Symbology))
                problems.Add($"The {label} has an unknown symbology. Use one of: " +
                    string.Join(", ", Enum.GetNames<Symbology>().Select(n => JsonNamingPolicy.CamelCase.ConvertName(n))) + ".");
            if (e is ImageElement image)
            {
                if (image.WidthMm <= 0)
                    problems.Add($"The {label} needs a widthMm larger than zero.");
                if (image.HeightMm is <= 0)
                    problems.Add($"The {label} has a heightMm of zero or less. Remove it to keep the picture's shape.");
                if (image.Threshold is < 0 or > 255)
                    problems.Add($"The {label} has a threshold of {image.Threshold}. Use 0 to 255.");
            }
            if (e is RepeatElement r)
            {
                if (string.IsNullOrWhiteSpace(r.Source))
                    problems.Add($"The {label} has no source. Set source to the name of the list in your data.");
                if (r.RowHeightMm <= 0)
                    problems.Add($"The {label} needs a rowHeightMm larger than zero.");
                ValidateElements(r.Elements, label + " > ", problems);
            }
        }
    }
}
