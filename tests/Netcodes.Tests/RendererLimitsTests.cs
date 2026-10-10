using System.Diagnostics;
using System.Text;
using Netcodes.Zpl;

namespace Netcodes.Tests;

/// <summary>Hostile or careless ZPL (it can come from an API caller) must not exhaust memory or time.</summary>
public class RendererLimitsTests
{
    [Fact]
    public void Thousands_of_fields_are_capped_with_a_warning()
    {
        var sb = new StringBuilder("^XA^PW400^LL300");
        for (var i = 0; i < 20000; i++) sb.Append("^FO0,0^GB10,10,1^FS");
        sb.Append("^XZ");
        var watch = Stopwatch.StartNew();
        var r = ZplRenderer.Render(sb.ToString());
        Assert.Contains(r.Warnings, w => w.Contains("too many fields"));
        Assert.True(watch.ElapsedMilliseconds < 5000);
    }

    [Fact]
    public void An_enormous_font_size_is_clamped_instead_of_exhausting_memory()
    {
        var watch = Stopwatch.StartNew();
        var r = ZplRenderer.Render("^XA^PW400^LL300^FO0,0^A0N,2000000000,2000000000^FDX^FS^XZ");
        Assert.NotEmpty(r.Png);
        Assert.True(watch.ElapsedMilliseconds < 5000);
    }

    [Fact]
    public void Very_long_barcode_data_is_skipped_with_a_warning()
    {
        var zpl = "^XA^PW400^LL300^FO0,0^BQN,2,10^FDQA," + new string('A', 200_000) + "^FS^XZ";
        var r = ZplRenderer.Render(zpl);
        Assert.Contains(r.Warnings, w => w.Contains("too long"));
    }
}
