using Netcodes.Zpl;

namespace Netcodes.Tests;

public class ParserTests
{
    private const string Sample =
        "^XA^PW812^LL1218" +
        "^FO50,60^A0N,40,40^FDHello^FS" +
        "^FO50,200^GB300,100,4^FS" +
        "^FO50,400^BY2^BCN,100,Y,N,N^FD12345^FS" +
        "^FO50,600^BQN,2,5^FDQA,HELLO^FS" +
        "^XZ";

    [Fact]
    public void Reads_label_size_and_the_basic_fields()
    {
        var doc = ZplParser.Parse(Sample);
        Assert.Equal(812, doc.WidthDots);
        Assert.Equal(1218, doc.HeightDots);
        Assert.Equal(4, doc.Ops.Count);

        var text = Assert.IsType<TextOp>(doc.Ops[0]);
        Assert.Equal((50, 60, "Hello", 40, 'N'), (text.X, text.Y, text.Text, text.HeightDots, text.Orientation));

        var box = Assert.IsType<BoxOp>(doc.Ops[1]);
        Assert.Equal((50, 200, 300, 100, 4), (box.X, box.Y, box.WidthDots, box.HeightDots, box.Thickness));

        var bc = Assert.IsType<BarcodeOp>(doc.Ops[2]);
        Assert.Equal(ZplSymbology.Code128, bc.Symbology);
        Assert.Equal(("12345", 100, 2, true), (bc.Data, bc.HeightDots, bc.ModuleWidth, bc.ShowText));

        var qr = Assert.IsType<BarcodeOp>(doc.Ops[3]);
        Assert.Equal((ZplSymbology.QrCode, "HELLO", 5, 'Q'), (qr.Symbology, qr.Data, qr.ModuleWidth, qr.QrErrorCorrection));
        Assert.Empty(doc.Warnings);
    }

    [Fact]
    public void Hex_escapes_after_FH_are_decoded()
    {
        var doc = ZplParser.Parse("^XA^FO0,0^FH_^FDa_5Eb_7E_C3_B1^FS^XZ");
        Assert.Equal("a^b~ñ", Assert.IsType<TextOp>(Assert.Single(doc.Ops)).Text);
    }

    [Fact]
    public void Reverse_and_baseline_flags_are_kept_per_field()
    {
        var doc = ZplParser.Parse("^XA^FO10,10^GB50,50,50^FR^FS^FT20,30^A0N,20,20^FDx^FS^FO0,0^FDy^FS^XZ");
        Assert.True(doc.Ops[0].Reverse);
        Assert.True(doc.Ops[1].Baseline);
        Assert.False(doc.Ops[1].Reverse);
        Assert.False(doc.Ops[2].Baseline);
    }

    [Fact]
    public void Field_block_is_attached_to_the_text()
    {
        var doc = ZplParser.Parse("^XA^FO0,0^FB400,3,5,C,0^A0N,30,30^FDwrap me^FS^XZ");
        var block = Assert.IsType<TextOp>(Assert.Single(doc.Ops)).Block;
        Assert.Equal(new ZplBlock(400, 3, 5, 'C'), block);
    }

    [Fact]
    public void Default_font_applies_when_a_field_has_no_font_command()
    {
        var doc = ZplParser.Parse("^XA^CF0,50,40^FO0,0^FDabc^FS^XZ");
        var t = Assert.IsType<TextOp>(Assert.Single(doc.Ops));
        Assert.Equal(("0", 50, 40), (t.Font, t.HeightDots, t.WidthDots));
    }

    [Fact]
    public void Gs1_mode_D_is_recognised_and_keeps_the_bracketed_text()
    {
        var doc = ZplParser.Parse("^XA^FO0,0^BY2^BCN,100,Y,N,N,D^FD(00)106141411234567897^FS^XZ");
        var bc = Assert.IsType<BarcodeOp>(Assert.Single(doc.Ops));
        Assert.Equal(ZplSymbology.Gs1128, bc.Symbology);
        Assert.Equal("(00)106141411234567897", bc.Data);
    }

    [Fact]
    public void Unknown_commands_become_one_warning_each_and_drawing_continues()
    {
        var doc = ZplParser.Parse("^XA^ZZ1^ZZ2~DGR:LOGO.GRF,10,5,AAAA^FO0,0^FDok^FS^XZ");
        Assert.Single(doc.Ops);
        Assert.Equal(2, doc.Warnings.Count);
        Assert.Contains(doc.Warnings, w => w.Contains("^ZZ") && w.Contains("not supported yet"));
        Assert.Contains(doc.Warnings, w => w.Contains("~DG"));
    }

    [Fact]
    public void Printer_control_commands_are_accepted_silently()
    {
        var doc = ZplParser.Parse("^XA^PR4^MD10^MTT^PQ3^FO0,0^FDok^FS^XZ");
        Assert.Single(doc.Ops);
        Assert.Empty(doc.Warnings);
    }

    [Fact]
    public void Only_the_first_label_is_previewed()
    {
        var doc = ZplParser.Parse("^XA^FO0,0^FDone^FS^XZ^XA^FO0,0^FDtwo^FS^XZ");
        Assert.Single(doc.Ops);
        Assert.Contains("more than one label", Assert.Single(doc.Warnings));
    }

    [Fact]
    public void Newlines_between_commands_are_ignored()
    {
        var doc = ZplParser.Parse("^XA\r\n^FO5,5\r\n^FDline^FS\r\n^XZ\r\n");
        Assert.Equal("line", Assert.IsType<TextOp>(Assert.Single(doc.Ops)).Text);
    }

    [Fact]
    public void Empty_input_gives_an_empty_document()
    {
        var doc = ZplParser.Parse("");
        Assert.Empty(doc.Ops);
        Assert.Null(doc.WidthDots);
    }
}
