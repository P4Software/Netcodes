using SkiaSharp;
using Netcodes;
using Netcodes.Printing;
using Netcodes.Template;

namespace Netcodes.Tests;

public class EngineTests
{
    private const string Template = """
    { "name":"demo","widthMm":100,"heightMm":150,"dpi":203,
      "elements":[
        {"type":"text","x":5,"y":5,"text":"Hello {{name}}","heightMm":6},
        {"type":"barcode","x":5,"y":30,"data":"{{code}}","heightMm":15},
        {"type":"qr","x":60,"y":30,"data":"https://x.test/{{code}}","size":4} ] }
    """;

    private const string Data = """{"name":"Ana","code":"123456"}""";

    [Fact]
    public async Task Generate_then_preview_then_dry_run_print()
    {
        var engine = new NetcodesEngine();
        var template = LabelTemplate.FromJson(Template);

        var zpl = engine.GenerateZpl(template, Data);
        Assert.Empty(zpl.Warnings);
        Assert.Contains("^FDHello Ana^FS", zpl.Zpl);

        var preview = engine.Preview(template, Data);
        Assert.Equal((799, 1199), (preview.WidthDots, preview.HeightDots));
        using var bmp = SKBitmap.Decode(preview.Png);
        Assert.Equal(799, bmp.Width);
        Assert.Empty(preview.Warnings);

        var print = await engine.PrintAsync(new PrinterTarget("Dock 1", "203.0.113.9"), zpl.Zpl);
        Assert.True(print.DryRun);   // safe by default: no dryRun argument means nothing is sent
    }

    [Fact]
    public void Raw_zpl_can_be_previewed_without_a_template()
    {
        var preview = new NetcodesEngine().Preview("^XA^PW200^LL100^FO10,10^GB50,50,50^FS^XZ");
        Assert.Equal((200, 100), (preview.WidthDots, preview.HeightDots));
    }
}
