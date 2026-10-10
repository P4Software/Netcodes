using Netcodes.Template;
using Netcodes.Zpl;

namespace Netcodes.Tests;

public class RoundTripTests
{
    [Fact]
    public void Generated_zpl_parses_back_to_the_same_elements()
    {
        var template = GeneratorTextBoxTests.Template(
            new TextElement { X = 5, Y = 5, Text = "Ship to: {{name}}", HeightMm = 4 },
            new BarcodeElement { X = 5, Y = 40, Data = "{{code}}", HeightMm = 15, ModuleWidth = 3 },
            new QrElement { X = 70, Y = 40, Data = "{{code}}", Size = 5, ErrorCorrection = "H" },
            new BoxElement { X = 2, Y = 2, WidthMm = 96, HeightMm = 146, BorderMm = 0.5 });

        var generated = ZplGenerator.Generate(template, """{"name":"Ana ^ Co","code":"123456"}""");
        var doc = ZplParser.Parse(generated.Zpl);

        Assert.Equal((799, 1199), (doc.WidthDots, doc.HeightDots));
        Assert.Empty(doc.Warnings);
        Assert.Equal(4, doc.Ops.Count);

        var text = Assert.IsType<TextOp>(doc.Ops[0]);
        Assert.Equal(("Ship to: Ana ^ Co", 40, 40, 32), (text.Text, text.X, text.Y, text.HeightDots));

        var barcode = Assert.IsType<BarcodeOp>(doc.Ops[1]);
        Assert.Equal((ZplSymbology.Code128, "123456", 120, 3), (barcode.Symbology, barcode.Data, barcode.HeightDots, barcode.ModuleWidth));

        var qr = Assert.IsType<BarcodeOp>(doc.Ops[2]);
        Assert.Equal((ZplSymbology.QrCode, "123456", 5, 'H'), (qr.Symbology, qr.Data, qr.ModuleWidth, qr.QrErrorCorrection));

        var box = Assert.IsType<BoxOp>(doc.Ops[3]);
        Assert.Equal((767, 1167, 4), (box.WidthDots, box.HeightDots, box.Thickness));
    }

    [Fact]
    public void A_gs1_label_survives_the_round_trip_and_renders_without_warnings()
    {
        var template = GeneratorTextBoxTests.Template(new Gs1Element
        {
            X = 5, Y = 30, HeightMm = 20, Fields = new() { ["sscc"] = "10614141123456789", ["lot"] = "LOT1" },
        });
        var generated = ZplGenerator.Generate(template, "{}");
        var op = Assert.IsType<BarcodeOp>(Assert.Single(ZplParser.Parse(generated.Zpl).Ops));
        Assert.Equal((ZplSymbology.Gs1128, "(00)106141411234567897(10)LOT1"), (op.Symbology, op.Data));
        Assert.Empty(ZplRenderer.Render(generated.Zpl).Warnings);
    }

    [Fact]
    public void A_full_template_with_every_element_previews_cleanly()
    {
        var template = GeneratorTextBoxTests.Template(
            new TextElement { X = 5, Y = 5, Text = "{{name}}", HeightMm = 6 },
            new BarcodeElement { X = 5, Y = 20, Data = "5901234123457", Symbology = Symbology.Ean13, HeightMm = 15 },
            new RepeatElement
            {
                X = 5, Y = 60, Source = "lines", RowHeightMm = 7,
                Elements = { new TextElement { Text = "{{item.sku}} x{{item.qty}}", HeightMm = 4 } },
            });
        var r = new NetcodesEngine().Preview(template, """{"name":"Ana","lines":[{"sku":"A1","qty":2},{"sku":"B2","qty":5}]}""");
        Assert.Empty(r.Warnings);
        Assert.NotEmpty(r.Png);
    }

    [Fact]
    public void Every_new_element_goes_from_template_to_a_warning_free_preview()
    {
        // A tiny transparent PNG for the logo; transparent prints white, so this only has to not warn.
        using var bmp = new SkiaSharp.SKBitmap(8, 8);
        using var image = SkiaSharp.SKImage.FromBitmap(bmp);
        using var encoded = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        var logo = Convert.ToBase64String(encoded.ToArray());

        var json = """
        {"name":"all","widthMm":100,"heightMm":150,"dpi":203,"elements":[
          {"type":"barcode","symbology":"pdf417","x":5,"y":5,"data":"PDF417 {{n}}","heightMm":15},
          {"type":"barcode","symbology":"code93","x":5,"y":30,"data":"HELLO","heightMm":10},
          {"type":"barcode","symbology":"codabar","x":5,"y":45,"data":"1234","heightMm":10},
          {"type":"barcode","symbology":"msi","x":5,"y":60,"data":"1234","heightMm":10},
          {"type":"barcode","symbology":"aztec","x":5,"y":75,"data":"AZTEC","moduleWidth":4},
          {"type":"image","x":50,"y":75,"widthMm":20,"data":"{{logo}}"}]}
        """;
        var data = $$"""{"n":"1","logo":"{{logo}}"}""";
        var r = new NetcodesEngine().Preview(LabelTemplate.FromJson(json), data);
        Assert.Empty(r.Warnings);
        Assert.NotEmpty(r.Png);
    }
}
