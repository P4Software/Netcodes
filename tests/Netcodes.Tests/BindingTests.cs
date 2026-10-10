using System.Text.Json;
using Netcodes.Template;

namespace Netcodes.Tests;

public class BindingTests
{
    private static DataContext Ctx(string json) => new(JsonDocument.Parse(json).RootElement.Clone());

    [Theory]
    [InlineData(100, 203, 799)]
    [InlineData(25.4, 203, 203)]
    [InlineData(0, 300, 0)]
    [InlineData(4, 203, 32)]
    public void Converts_millimetres_to_dots(double mm, int dpi, int expected)
    {
        Assert.Equal(expected, Dots.FromMm(mm, dpi));
    }

    [Fact]
    public void Expands_dotted_paths()
    {
        var warnings = new List<string>();
        var text = Ctx("""{"customer":{"name":"Ana"},"qty":3,"rush":true}""")
            .Expand("To: {{customer.name}} x{{qty}} rush={{ rush }}", warnings);
        Assert.Equal("To: Ana x3 rush=true", text);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Missing_field_prints_empty_and_warns_in_plain_language()
    {
        var warnings = new List<string>();
        var text = Ctx("""{"customer":{}}""").Expand("[{{customer.name}}]", warnings);
        Assert.Equal("[]", text);
        var w = Assert.Single(warnings);
        Assert.Contains("{{customer.name}}", w);
        Assert.Contains("has no value", w);
    }

    [Fact]
    public void Null_and_object_values_print_empty_with_a_warning()
    {
        var warnings = new List<string>();
        var text = Ctx("""{"a":null,"b":{"x":1}}""").Expand("{{a}}|{{b}}", warnings);
        Assert.Equal("|", text);
        Assert.Equal(2, warnings.Count);
    }

    [Fact]
    public void Text_without_placeholders_is_returned_unchanged()
    {
        Assert.Equal("plain", Ctx("{}").Expand("plain", new List<string>()));
    }

    [Fact]
    public void Item_scope_reads_the_current_row_and_index()
    {
        var warnings = new List<string>();
        var root = Ctx("""{"lines":[{"sku":"A1"},{"sku":"B2"}],"order":"SO-9"}""");
        var items = root.GetArray("lines", warnings);
        Assert.Equal(2, items.Count);
        var row = root.ForItem(items[1], 1);
        Assert.Equal("2: B2 / SO-9", row.Expand("{{index}}: {{item.sku}} / {{order}}", warnings));
    }

    [Fact]
    public void Missing_list_returns_no_rows_and_warns()
    {
        var warnings = new List<string>();
        Assert.Empty(Ctx("{}").GetArray("lines", warnings));
        Assert.Contains("lines", Assert.Single(warnings));
    }
}
