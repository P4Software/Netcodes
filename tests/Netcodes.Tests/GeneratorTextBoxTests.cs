using Netcodes.Template;
using Netcodes.Zpl;

namespace Netcodes.Tests;

public class GeneratorTextBoxTests
{
    internal static LabelTemplate Template(params LabelElement[] elements) => new()
    {
        Name = "t", WidthMm = 100, HeightMm = 150, Dpi = 203, Elements = elements.ToList(),
    };

    [Fact]
    public void Writes_header_text_and_footer()
    {
        var t = Template(new TextElement { X = 5, Y = 5, Text = "Ship to: {{name}}", HeightMm = 4 });
        var r = ZplGenerator.Generate(t, """{"name":"Ana"}""");
        Assert.StartsWith("^XA\n^CI28\n^PW799\n^LL1199\n^LH0,0\n", r.Zpl);
        Assert.Contains("^FO40,40^A0N,32,32^FDShip to: Ana^FS\n", r.Zpl);
        Assert.EndsWith("^XZ\n", r.Zpl);
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void Special_characters_are_hex_escaped_so_they_cannot_break_the_zpl()
    {
        var t = Template(new TextElement { Text = "Niño ^Co_x~", HeightMm = 4 });
        var r = ZplGenerator.Generate(t, "{}");
        Assert.Contains("^FH_^FDNiño _5ECo_5Fx_7E^FS", r.Zpl);
    }

    [Fact]
    public void Rotation_selects_the_zpl_orientation()
    {
        var t = Template(new TextElement { Text = "x", HeightMm = 4, Rotation = 90 });
        Assert.Contains("^A0R,32,32", ZplGenerator.Generate(t, "{}").Zpl);
    }

    [Fact]
    public void Wrapped_text_uses_a_field_block()
    {
        var t = Template(new TextElement { Text = "long", HeightMm = 4, MaxWidthMm = 50, MaxLines = 3 });
        Assert.Contains("^FB400,3,0,L,0^A0N", ZplGenerator.Generate(t, "{}").Zpl);
    }

    [Fact]
    public void Outline_and_filled_boxes()
    {
        var outline = Template(new BoxElement { X = 2, Y = 2, WidthMm = 96, HeightMm = 146, BorderMm = 0.5 });
        Assert.Contains("^FO16,16^GB767,1167,4^FS\n", ZplGenerator.Generate(outline, "{}").Zpl);
        var filled = Template(new BoxElement { X = 2, Y = 2, WidthMm = 96, HeightMm = 146, Filled = true });
        Assert.Contains("^FO16,16^GB767,1167,767^FS\n", ZplGenerator.Generate(filled, "{}").Zpl);
    }

    [Fact]
    public void Missing_data_warns_but_still_produces_a_label()
    {
        var t = Template(new TextElement { Text = "{{missing}}", HeightMm = 4 });
        var r = ZplGenerator.Generate(t, "{}");
        Assert.Contains("^FD^FS", r.Zpl);
        Assert.Contains("{{missing}}", Assert.Single(r.Warnings));
    }

    [Fact]
    public void An_invalid_template_is_refused_with_all_problems_listed()
    {
        var ex = Assert.Throws<TemplateException>(() => ZplGenerator.Generate(new LabelTemplate(), "{}"));
        Assert.Contains("no name", ex.Message);
    }

    [Fact]
    public void Broken_data_json_gets_a_plain_message()
    {
        var ex = Assert.Throws<TemplateException>(() => ZplGenerator.Generate(Template(), "{ nope"));
        Assert.Contains("data is not valid JSON", ex.Message);
    }
}
