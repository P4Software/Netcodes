using Netcodes.Template;
using Netcodes.Zpl;

namespace Netcodes.Tests;

/// <summary>Code 93, Codabar, MSI and Aztec (Plan 1b).</summary>
public class ExtraBarcodeTests
{
    private static readonly ZXingBarcodeEncoder Encoder = new();

    private static ZplResult Generate(Symbology symbology, string data) =>
        ZplGenerator.Generate(GeneratorTextBoxTests.Template(
            new BarcodeElement { Symbology = symbology, Data = data, HeightMm = 15 }), "{}");

    [Fact]
    public void Code93_is_9_modules_per_character_plus_start_two_checks_stop_and_a_termination_bar()
    {
        var m = Encoder.Encode(ZplSymbology.Code93, "HELLO");
        Assert.Equal((9 * (5 + 4) + 1, 1), (m.Width, m.Height));
    }

    [Fact]
    public void Codabar_without_start_and_stop_letters_gets_A_on_both_ends()
    {
        var plain = Encoder.Encode(ZplSymbology.Codabar, "1234");
        var explicitAA = Encoder.Encode(ZplSymbology.Codabar, "A1234A");
        Assert.Equal(explicitAA.Width, plain.Width);
    }

    [Fact]
    public void Msi_encodes_digits_and_Aztec_is_square()
    {
        Assert.True(Encoder.Encode(ZplSymbology.Msi, "1234").Width > 40);
        var aztec = Encoder.Encode(ZplSymbology.Aztec, "HELLO");
        Assert.Equal(aztec.Width, aztec.Height);
        Assert.True(aztec.Width >= 15);
    }

    [Theory]
    [InlineData(Symbology.Code93, "HELLO", "^BAN,120,Y,N,N^FDHELLO^FS")]
    [InlineData(Symbology.Codabar, "1234", "^BKN,N,120,Y,N,A,A^FD1234^FS")]
    [InlineData(Symbology.Aztec, "HELLO", "^BON,2,N,0,N,1^FDHELLO^FS")]
    public void The_generator_writes_each_symbology(Symbology symbology, string data, string expected) =>
        Assert.Contains(expected, Generate(symbology, data).Zpl);

    [Fact]
    public void Msi_is_written_with_the_check_digit_setting_that_matches_the_preview()
    {
        // The preview encoder decides whether a check digit is in the bars; the ZPL must ask the printer for the same.
        var zpl = Generate(Symbology.Msi, "1234").Zpl;
        // Probed: ZXing draws no check digit (4 digits = 55 modules), so the printer must be told "A" (none), not B (Mod 10).
        Assert.Contains("^BMN,A,120,Y,N,N^FD1234^FS", zpl);
    }

    [Theory]
    [InlineData(Symbology.Msi, "12AB", "MSI")]
    [InlineData(Symbology.Codabar, "12 34", "Codabar")]
    [InlineData(Symbology.Code93, "abc", "Code 93")]
    public void Bad_data_leaves_that_barcode_out_with_a_warning_and_keeps_the_label(Symbology symbology, string data, string mention)
    {
        var r = ZplGenerator.Generate(GeneratorTextBoxTests.Template(
            new TextElement { Text = "keep me", HeightMm = 4 },
            new BarcodeElement { Symbology = symbology, Data = data, HeightMm = 15 }), "{}");
        Assert.Contains("keep me", r.Zpl);
        Assert.DoesNotContain("^BA", r.Zpl);
        Assert.DoesNotContain("^BK", r.Zpl);
        Assert.DoesNotContain("^BM", r.Zpl);
        Assert.Contains(r.Warnings, w => w.Contains(mention));
    }

    [Fact]
    public void The_parser_reads_the_new_commands()
    {
        var doc = ZplParser.Parse("^XA^BY3^FO10,10^BAN,80,Y,N,N^FDABC^FS^FO10,200^BKN,N,60,N,N,A,A^FD123^FS" +
                                  "^FO10,300^BMN,B,70,Y,N,N^FD99^FS^FO10,400^BON,5,N,0,N,1^FDHI^FS^XZ");
        var ops = doc.Ops.Cast<BarcodeOp>().ToList();
        Assert.Equal(new[] { ZplSymbology.Code93, ZplSymbology.Codabar, ZplSymbology.Msi, ZplSymbology.Aztec },
            ops.Select(o => o.Symbology));
        Assert.Equal((80, 3), (ops[0].HeightDots, ops[0].ModuleWidth));
        Assert.Equal(60, ops[1].HeightDots);
        Assert.Equal(70, ops[2].HeightDots);
        Assert.Equal(5, ops[3].ModuleWidth);          // for Aztec the second slot is the magnification
    }

    [Fact]
    public void The_preview_draws_every_new_symbology()
    {
        var zpl = "^XA^PW500^LL500^BY2^FO10,10^BAN,60,Y,N,N^FDABC^FS^FO10,100^BKN,N,60,Y,N,A,A^FD123^FS" +
                  "^FO10,200^BMN,B,60,Y,N,N^FD99^FS^FO10,300^BON,4,N,0,N,1^FDHELLO^FS^XZ";
        var r = ZplRenderer.Render(zpl);
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void New_symbologies_survive_template_json()
    {
        var json = LabelTemplate.FromJson(
            """{"name":"x","widthMm":50,"heightMm":30,"elements":[{"type":"barcode","symbology":"code93","data":"A"},{"type":"barcode","symbology":"aztec","data":"A"}]}""")
            .ToJson();
        Assert.Contains("code93", json);
        Assert.Contains("aztec", json);
    }
}
