using Netcodes.Template;
using Netcodes.Zpl;

namespace Netcodes.Tests;

public class GeneratorRepeatTests
{
    private static RepeatElement Repeat() => new()
    {
        X = 0, Y = 100, Source = "lines", RowHeightMm = 6,
        Elements = { new TextElement { X = 5, Y = 0, Text = "{{index}}. {{item.sku}}", HeightMm = 4 } },
    };

    [Fact]
    public void Prints_one_row_per_item_stepping_down_by_the_row_height()
    {
        var r = ZplGenerator.Generate(GeneratorTextBoxTests.Template(Repeat()),
            """{"lines":[{"sku":"A1"},{"sku":"B2"},{"sku":"C3"}]}""");
        Assert.Contains("^FO40,799^A0N,32,32^FD1. A1^FS", r.Zpl);
        Assert.Contains("^FO40,847^A0N,32,32^FD2. B2^FS", r.Zpl);
        Assert.Contains("^FO40,895^A0N,32,32^FD3. C3^FS", r.Zpl);
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void An_empty_list_prints_no_rows_without_complaint()
    {
        var r = ZplGenerator.Generate(GeneratorTextBoxTests.Template(Repeat()), """{"lines":[]}""");
        Assert.DoesNotContain("^FD", r.Zpl);
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void A_missing_list_warns()
    {
        var r = ZplGenerator.Generate(GeneratorTextBoxTests.Template(Repeat()), "{}");
        Assert.Contains(r.Warnings, w => w.Contains("lines"));
    }

    [Fact]
    public void Rows_that_do_not_fit_on_the_label_are_dropped_with_a_count()
    {
        var items = string.Join(",", Enumerable.Range(1, 30).Select(i => $"{{\"sku\":\"S{i}\"}}"));
        var r = ZplGenerator.Generate(GeneratorTextBoxTests.Template(Repeat()), $"{{\"lines\":[{items}]}}");
        Assert.Equal(8, r.Zpl.Split("^FD").Length - 1);
        Assert.Contains("Only 8 of 30 rows fit", Assert.Single(r.Warnings));
    }

    [Fact]
    public void Max_rows_caps_the_output()
    {
        var rep = Repeat();
        rep.MaxRows = 2;
        var r = ZplGenerator.Generate(GeneratorTextBoxTests.Template(rep),
            """{"lines":[{"sku":"A"},{"sku":"B"},{"sku":"C"}]}""");
        Assert.Equal(2, r.Zpl.Split("^FD").Length - 1);
        Assert.Contains("limit of 2", Assert.Single(r.Warnings));
    }
}

public class GeneratorLimitsTests
{
    // Review finding: a row height that rounds to 0 dots never "runs off" the label, so the
    // per-label height check could not stop a huge list (or nested repeats) from exploding.
    [Fact]
    public void A_tiny_row_height_cannot_produce_an_unbounded_label()
    {
        var rep = new RepeatElement
        {
            Source = "lines", RowHeightMm = 0.001, MaxRows = int.MaxValue,
            Elements = { new TextElement { Text = "{{item.sku}}", HeightMm = 4 } },
        };
        var items = string.Join(",", Enumerable.Range(1, 20000).Select(i => $"{{\"sku\":\"S{i}\"}}"));
        var r = ZplGenerator.Generate(GeneratorTextBoxTests.Template(rep), $"{{\"lines\":[{items}]}}");
        Assert.True(r.Zpl.Split("^FD").Length - 1 <= 5000);
        Assert.Contains(r.Warnings, w => w.Contains("too many fields"));
    }

    [Fact]
    public void Nested_repeats_share_the_same_field_budget()
    {
        var inner = new RepeatElement
        {
            Source = "item.parts", RowHeightMm = 0.001,
            Elements = { new TextElement { Text = "p", HeightMm = 4 } },
        };
        var outer = new RepeatElement { Source = "lines", RowHeightMm = 0.001, Elements = { inner } };
        var parts = string.Join(",", Enumerable.Repeat("1", 200));
        var rows = string.Join(",", Enumerable.Repeat($"{{\"parts\":[{parts}]}}", 200));
        var r = ZplGenerator.Generate(GeneratorTextBoxTests.Template(outer), $"{{\"lines\":[{rows}]}}");
        Assert.True(r.Zpl.Split("^FD").Length - 1 <= 5000);
        Assert.Contains(r.Warnings, w => w.Contains("too many fields"));
    }
}

public class GeneratorEmptyRepeatLimitTests
{
    // Review finding: repeats nested with no children write no fields, so a field-only budget never fired.
    // Source paths that do not start with "item." resolve from the root, so every level loops over the same list.
    [Fact]
    public void Deeply_nested_empty_repeats_stop_at_the_budget_instead_of_looping_forever()
    {
        RepeatElement Level(params LabelElement[] inner)
        {
            var r = new RepeatElement { Source = "lines", RowHeightMm = 0.001 };
            r.Elements.AddRange(inner);
            return r;
        }
        var nested = Level(Level(Level(Level())));
        var rows = string.Join(",", Enumerable.Repeat("{}", 100));
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var r = ZplGenerator.Generate(GeneratorTextBoxTests.Template(nested), $"{{\"lines\":[{rows}]}}");
        watch.Stop();
        Assert.Contains(r.Warnings, w => w.Contains("too many fields"));
        Assert.True(watch.ElapsedMilliseconds < 2000);
    }
}
