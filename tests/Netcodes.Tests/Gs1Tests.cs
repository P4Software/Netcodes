using Netcodes.Template;
using Netcodes.Zpl;

namespace Netcodes.Tests;

public class Gs1Tests
{
    [Theory]
    [InlineData("629104150021", 3)]          // EAN-13 6291041500213
    [InlineData("10614141123456789", 7)]     // GS1 SSCC example 106141411234567897
    public void Check_digit_matches_published_examples(string digits, int expected)
    {
        Assert.Equal(expected, Gs1.CheckDigit(digits));
    }

    [Fact]
    public void Sscc_of_17_digits_gets_its_check_digit()
    {
        var w = new List<string>();
        Assert.Equal("(00)106141411234567897", Gs1.Build(new Dictionary<string, string> { ["sscc"] = "10614141123456789" }, w));
        Assert.Empty(w);
    }

    [Fact]
    public void A_wrong_check_digit_is_reported_but_not_changed()
    {
        var w = new List<string>();
        var text = Gs1.Build(new Dictionary<string, string> { ["sscc"] = "106141411234567890" }, w);
        Assert.Equal("(00)106141411234567890", text);
        Assert.Contains("check digit", Assert.Single(w));
        Assert.Contains("7", w[0]);
    }

    [Fact]
    public void Sscc_with_the_wrong_length_is_refused_in_plain_language()
    {
        var ex = Assert.Throws<TemplateException>(() =>
            Gs1.Build(new Dictionary<string, string> { ["sscc"] = "123" }, new List<string>()));
        Assert.Contains("17 digits", ex.Message);
    }

    [Fact]
    public void Gtin_of_13_digits_becomes_gtin14_and_variable_fields_come_last()
    {
        var w = new List<string>();
        var text = Gs1.Build(new Dictionary<string, string>
        {
            ["lot"] = "LOT1", ["gtin"] = "0629104150021", ["expiry"] = "261231",
        }, w);
        Assert.Equal("(01)06291041500213(17)261231(10)LOT1", text);
    }

    [Fact]
    public void Unknown_field_names_are_skipped_with_a_warning()
    {
        var w = new List<string>();
        Gs1.Build(new Dictionary<string, string> { ["sscc"] = "10614141123456789", ["colour"] = "red" }, w);
        Assert.Contains("colour", Assert.Single(w));
    }

    [Fact]
    public void Encoder_data_puts_fnc1_only_after_variable_length_fields()
    {
        Assert.Equal("00106141411234567897" + "10LOT1" + "ñ" + "21A9",
            Gs1.ToEncoderData("(00)106141411234567897(10)LOT1(21)A9"));
    }

    [Fact]
    public void Generator_writes_gs1_mode_d()
    {
        var e = new Gs1Element
        {
            X = 5, Y = 60, HeightMm = 15,
            Fields = new() { ["sscc"] = "{{sscc}}", ["lot"] = "LOT1" },
        };
        var zpl = ZplGenerator.Generate(GeneratorTextBoxTests.Template(e), """{"sscc":"10614141123456789"}""").Zpl;
        Assert.Contains("^BY2,3,120^FO40,480^BCN,120,Y,N,N,D^FD(00)106141411234567897(10)LOT1^FS\n", zpl);
    }
}
