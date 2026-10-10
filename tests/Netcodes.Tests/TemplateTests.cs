using Netcodes.Template;

namespace Netcodes.Tests;

public class TemplateTests
{
    private const string Example = """
    {
      "name": "shipping-label",
      "widthMm": 100, "heightMm": 150, "dpi": 203,
      "elements": [
        { "type": "text",    "x": 5, "y": 5,  "text": "Ship to: {{customer.name}}", "font": "0", "heightMm": 4 },
        { "type": "barcode", "x": 5, "y": 40, "symbology": "code128", "data": "{{sscc}}", "heightMm": 15, "showText": true },
        { "type": "qr",      "x": 70, "y": 40, "data": "{{trackingUrl}}", "size": 4 },
        { "type": "box",     "x": 2, "y": 2, "widthMm": 96, "heightMm": 146, "borderMm": 0.5 }
      ]
    }
    """;

    [Fact]
    public void Loads_the_example_from_the_spec()
    {
        var t = LabelTemplate.FromJson(Example);
        Assert.Equal("shipping-label", t.Name);
        Assert.Equal(4, t.Elements.Count);
        Assert.IsType<TextElement>(t.Elements[0]);
        Assert.Equal(Symbology.Code128, Assert.IsType<BarcodeElement>(t.Elements[1]).Symbology);
        Assert.Equal(4, Assert.IsType<QrElement>(t.Elements[2]).Size);
        Assert.Equal(0.5, Assert.IsType<BoxElement>(t.Elements[3]).BorderMm);
    }

    [Fact]
    public void Round_trips_through_json()
    {
        var t = LabelTemplate.FromJson(Example);
        Assert.Equal(t.ToJson(), LabelTemplate.FromJson(t.ToJson()).ToJson());
    }

    [Fact]
    public void Broken_json_gets_a_plain_language_message()
    {
        var ex = Assert.Throws<TemplateException>(() => LabelTemplate.FromJson("{ \"name\": "));
        Assert.Contains("not valid JSON", ex.Message);
        Assert.Contains("comma or quote", ex.Message);
    }

    [Fact]
    public void Unknown_element_type_is_reported_not_thrown_raw()
    {
        const string json = """{ "name":"x","widthMm":10,"heightMm":10,"elements":[{"type":"hologram"}] }""";
        Assert.Throws<TemplateException>(() => LabelTemplate.FromJson(json));
    }

    [Fact]
    public void Validate_lists_every_problem_in_plain_language()
    {
        var t = new LabelTemplate { Name = "", WidthMm = 0, HeightMm = 10, Dpi = 99 };
        t.Elements.Add(new TextElement { X = -1, Rotation = 45, HeightMm = 0 });
        var problems = t.Validate();
        Assert.Contains(problems, p => p.Contains("no name"));
        Assert.Contains(problems, p => p.Contains("larger than zero"));
        Assert.Contains(problems, p => p.Contains("99 dpi"));
        Assert.Contains(problems, p => p.Contains("negative position"));
        Assert.Contains(problems, p => p.Contains("rotation 45"));
        Assert.Contains(problems, p => p.Contains("heightMm"));
    }

    [Fact]
    public void A_valid_template_has_no_problems()
    {
        Assert.Empty(LabelTemplate.FromJson(Example).Validate());
    }
}
