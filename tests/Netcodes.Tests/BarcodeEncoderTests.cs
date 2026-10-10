using Netcodes.Zpl;

namespace Netcodes.Tests;

public class BarcodeEncoderTests
{
    private static readonly ZXingBarcodeEncoder Encoder = new();

    [Fact]
    public void Code128_numeric_data_is_exactly_68_modules_wide()
    {
        // start C (11) + three digit pairs (33) + check (11) + stop (13); no quiet zone, one column per module.
        var m = Encoder.Encode(ZplSymbology.Code128, "123456");
        Assert.Equal((68, 1), (m.Width, m.Height));
        Assert.True(m[0, 0] && m[1, 0] && !m[2, 0]);   // start C begins 11010011100
    }

    [Fact]
    public void Ean13_is_95_modules_wide()
    {
        Assert.Equal(95, Encoder.Encode(ZplSymbology.Ean13, "629104150021").Width);
    }

    [Fact]
    public void Qr_version_1_is_21_modules_square_without_a_quiet_zone()
    {
        var m = Encoder.Encode(ZplSymbology.QrCode, "HELLO", 'L');
        Assert.Equal((21, 21), (m.Width, m.Height));
        Assert.True(m[0, 0]);      // finder pattern corner
    }

    [Fact]
    public void Gs1128_starts_with_fnc1_and_drops_the_brackets()
    {
        // start C (11) + FNC1 (11) + ten pairs (110) + check (11) + stop (13)
        Assert.Equal(156, Encoder.Encode(ZplSymbology.Gs1128, "(00)106141411234567897").Width);
    }

    [Fact]
    public void Data_matrix_is_square()
    {
        var m = Encoder.Encode(ZplSymbology.DataMatrix, "HELLO");
        Assert.Equal(m.Width, m.Height);
        Assert.True(m.Width >= 10);
    }

    [Fact]
    public void Interleaved_2_of_5_pads_an_odd_length_with_a_leading_zero()
    {
        Assert.Equal(Encoder.Encode(ZplSymbology.Itf, "0123").Width, Encoder.Encode(ZplSymbology.Itf, "123").Width);
    }

    [Theory]
    [InlineData(ZplSymbology.Ean13, "abc", "EAN-13")]
    [InlineData(ZplSymbology.UpcA, "12", "UPC-A")]
    [InlineData(ZplSymbology.Itf, "12a4", "Interleaved 2 of 5")]
    [InlineData(ZplSymbology.Code39, "lower", "Code 39")]
    [InlineData(ZplSymbology.Gs1128, "no brackets", "GS1-128")]
    [InlineData(ZplSymbology.Code128, "", "no data")]
    public void Invalid_data_gets_a_plain_language_message(ZplSymbology symbology, string data, string mention)
    {
        var ex = Assert.Throws<BarcodeException>(() => Encoder.Encode(symbology, data));
        Assert.Contains(mention, ex.Message);
    }

    [Fact]
    public void Ean13_with_a_bad_check_digit_is_reported_not_crashed()
    {
        var ex = Assert.Throws<BarcodeException>(() => Encoder.Encode(ZplSymbology.Ean13, "6291041500210"));
        Assert.Contains("EAN-13", ex.Message);
    }
}
