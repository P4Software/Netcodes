using Netcodes.Template;
using Netcodes.Zpl;

namespace Netcodes.Tests;

public class GeneratorBarcodeTests
{
    private static string Zpl(LabelElement e, string data = "{}") =>
        ZplGenerator.Generate(GeneratorTextBoxTests.Template(e), data).Zpl;

    [Fact]
    public void Code128_with_placeholder_data()
    {
        var zpl = Zpl(new BarcodeElement { X = 5, Y = 40, Data = "{{sscc}}", HeightMm = 15 },
            """{"sscc":"00012345678901234567"}""");
        Assert.Contains("^BY2,3,120^FO40,320^BCN,120,Y,N,N,A^FD00012345678901234567^FS\n", zpl);
    }

    [Theory]
    // Data must be valid for its symbology now that the generator checks it (EAN-13 and UPC-A need their full digits).
    [InlineData(Symbology.Code39, "123456", "^B3N,N,120,Y,N")]
    [InlineData(Symbology.Ean13, "629104150021", "^BEN,120,Y,N")]
    [InlineData(Symbology.UpcA, "01234567890", "^BUN,120,Y,N,Y")]
    [InlineData(Symbology.Itf, "123456", "^B2N,120,Y,N,N")]
    [InlineData(Symbology.DataMatrix, "123456", "^BXN,2,200")]
    public void Each_symbology_uses_its_own_command(Symbology symbology, string data, string command)
    {
        var zpl = Zpl(new BarcodeElement { Symbology = symbology, Data = data, HeightMm = 15 });
        Assert.Contains(command + "^FD" + data + "^FS", zpl);
    }

    [Fact]
    public void Hiding_the_text_turns_the_interpretation_line_off()
    {
        var zpl = Zpl(new BarcodeElement { Data = "1", HeightMm = 15, ShowText = false });
        Assert.Contains("^BCN,120,N,N,N,A", zpl);
    }

    [Fact]
    public void Module_width_is_clamped_to_the_printable_range()
    {
        var zpl = Zpl(new BarcodeElement { Data = "1", HeightMm = 15, ModuleWidth = 99 });
        Assert.Contains("^BY10,3,120", zpl);
    }

    [Fact]
    public void A_barcode_without_data_is_left_out_with_a_warning()
    {
        var r = ZplGenerator.Generate(GeneratorTextBoxTests.Template(new BarcodeElement { Data = "{{nope}}" }), "{}");
        Assert.DoesNotContain("^BC", r.Zpl);
        Assert.Contains(r.Warnings, w => w.Contains("no data"));
    }

    [Fact]
    public void Qr_code_puts_error_correction_and_mode_in_the_field_data()
    {
        var zpl = Zpl(new QrElement { X = 70, Y = 40, Data = "{{u}}", Size = 4 }, """{"u":"https://x.test/1"}""");
        Assert.Contains("^FO559,320^BQN,2,4^FDQA,https://x.test/1^FS\n", zpl);
    }

    [Fact]
    public void Unknown_qr_error_correction_falls_back_to_Q_with_a_warning()
    {
        var r = ZplGenerator.Generate(
            GeneratorTextBoxTests.Template(new QrElement { Data = "a", ErrorCorrection = "Z" }), "{}");
        Assert.Contains("^FDQA,a^FS", r.Zpl);
        Assert.Contains(r.Warnings, w => w.Contains("error correction"));
    }
}
