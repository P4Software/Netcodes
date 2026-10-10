using System.Diagnostics;
using Netcodes.Template;
using Netcodes.Zpl;

namespace Netcodes.Tests;

/// <summary>
/// Security review: every barcode was capped (8000 characters) but a label can hold thousands of barcodes, and a PDF417
/// is encoded several times while its column count is chosen. One label gets one budget for barcode work.
/// </summary>
public class BarcodeWorkBudgetTests
{
    private static string Data(int length) =>
        string.Concat(Enumerable.Range(0, length).Select(i => (char)('A' + (i * 7 + i / 3) % 26)));

    [Fact]
    public void Many_large_barcodes_in_zpl_share_one_budget_in_the_preview()
    {
        var field = $"^FO0,0^B7N,8,0,,,N^FD{Data(1000)}^FS";
        var zpl = "^XA^PW800^LL800" + string.Concat(Enumerable.Repeat(field, 900)) + "^XZ";
        var watch = Stopwatch.StartNew();
        var r = ZplRenderer.Render(zpl);
        Assert.True(watch.ElapsedMilliseconds < 8000, $"took {watch.ElapsedMilliseconds} ms");
        Assert.Contains(r.Warnings, w => w.Contains("too much barcode data"));
    }

    [Fact]
    public void Many_large_barcodes_in_a_template_share_one_budget_in_the_generator()
    {
        var elements = Enumerable.Range(0, 900)
            .Select(i => (LabelElement)new BarcodeElement { Symbology = Symbology.Pdf417, Data = Data(1000), HeightMm = 10 })
            .ToArray();
        var watch = Stopwatch.StartNew();
        var r = ZplGenerator.Generate(GeneratorTextBoxTests.Template(elements), "{}");
        Assert.True(watch.ElapsedMilliseconds < 8000, $"took {watch.ElapsedMilliseconds} ms");
        Assert.Contains(r.Warnings, w => w.Contains("too much barcode data"));
    }

    [Fact]
    public void An_ordinary_label_never_meets_the_budget()
    {
        var elements = new LabelElement[]
        {
            new BarcodeElement { Symbology = Symbology.Pdf417, Data = Data(200), HeightMm = 20 },
            new QrElement { Data = Data(500) },
            new BarcodeElement { Symbology = Symbology.Code128, Data = "123456", HeightMm = 12 },
        };
        var generated = ZplGenerator.Generate(GeneratorTextBoxTests.Template(elements), "{}");
        Assert.Empty(generated.Warnings);
        Assert.Empty(ZplRenderer.Render(generated.Zpl).Warnings);
    }
}
