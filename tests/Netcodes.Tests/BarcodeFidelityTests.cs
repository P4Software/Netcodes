using Netcodes.Zpl;

namespace Netcodes.Tests;

/// <summary>
/// Barcode shapes that must match what printers (and Labelary) produce. Every number below was measured from
/// Labelary's output for the same ZPL, then reproduced here.
/// </summary>
public class BarcodeFidelityTests
{
    private static readonly ZXingBarcodeEncoder Encoder = new();

    // ZPL's ^BY ratio defaults to 3: a wide bar is three times a narrow one. ZXing draws wide as two, so the
    // two-width codes (Code 39, Codabar, MSI) came out about 20 percent too narrow in the preview.
    [Fact]
    public void Code39_at_the_default_ratio_has_wide_elements_three_modules_wide()
    {
        // *AB*: four characters of 6 narrow + 3 wide elements (6 + 9 = 15 modules), plus three narrow gaps.
        Assert.Equal(4 * 15 + 3, Encoder.Encode(ZplSymbology.Code39, "AB").Width);
    }

    [Fact]
    public void A_ratio_of_two_keeps_wide_elements_two_modules_wide()
    {
        var options = new BarcodeOptions(WideToNarrowRatio: 2.0);
        Assert.Equal(4 * 12 + 3, Encoder.Encode(ZplSymbology.Code39, "AB", options: options).Width);
    }

    [Fact]
    public void Msi_at_ratio_three_is_start_4_plus_16_per_digit_plus_stop_5()
    {
        Assert.Equal(4 + 4 * 16 + 5, Encoder.Encode(ZplSymbology.Msi, "1234").Width);
    }

    [Fact]
    public void Codabar_gets_wider_with_the_ratio()
    {
        var three = Encoder.Encode(ZplSymbology.Codabar, "1234").Width;
        var two = Encoder.Encode(ZplSymbology.Codabar, "1234", options: new BarcodeOptions(WideToNarrowRatio: 2.0)).Width;
        Assert.True(three > two);
    }

    [Fact]
    public void Itf_and_Code128_are_not_changed_by_the_ratio()
    {
        var two = new BarcodeOptions(WideToNarrowRatio: 2.0);
        Assert.Equal(Encoder.Encode(ZplSymbology.Itf, "1234").Width, Encoder.Encode(ZplSymbology.Itf, "1234", options: two).Width);
        Assert.Equal(Encoder.Encode(ZplSymbology.Code128, "1234").Width, Encoder.Encode(ZplSymbology.Code128, "1234", options: two).Width);
    }

    // PDF417 shape, measured from Labelary at ^BY2 and ^B7N,8 (8-dot rows): width in modules and height in rows.
    [Theory]
    [InlineData("Netcodes PDF417 test 12345", 86, 20)]    // one data column; Labelary draws 19 rows (ZXing needs one more codeword)
    [InlineData("HELLO", 86, 6)]
    public void Pdf417_uses_the_smallest_column_count_that_fits_in_about_20_rows(string data, int modules, int rows)
    {
        var m = Encoder.Encode(ZplSymbology.Pdf417, data);
        Assert.Equal((modules, rows), (m.Width, m.Height));
    }

    [Fact]
    public void Pdf417_with_more_data_adds_columns_not_rows()
    {
        var data = new string('A', 60) + "123456789012345678901234567890123456789012345678901234567890";
        var m = Encoder.Encode(ZplSymbology.Pdf417, data);
        Assert.Equal(120, m.Width);              // three data columns: 69 + 3 x 17
        Assert.InRange(m.Height, 17, 19);
    }

    [Fact]
    public void Pdf417_security_level_and_columns_are_honoured()
    {
        const string data = "Netcodes PDF417 test 12345";
        var secure = Encoder.Encode(ZplSymbology.Pdf417, data, options: new BarcodeOptions(Pdf417Security: 4));
        Assert.Equal((120, 17), (secure.Width, secure.Height));
        var threeColumns = Encoder.Encode(ZplSymbology.Pdf417, data, options: new BarcodeOptions(Pdf417Columns: 3));
        Assert.Equal((120, 7), (threeColumns.Width, threeColumns.Height));
    }

    [Fact]
    public void The_parser_reads_the_ratio_and_the_pdf417_settings()
    {
        var doc = ZplParser.Parse("^XA^BY2,2.5^FO0,0^BCN,50,N,N,N,A^FD1^FS^BY3,3^FO0,100^B7N,8,4,3,,N^FDHELLO^FS^XZ");
        var code128 = Assert.IsType<BarcodeOp>(doc.Ops[0]);
        var pdf = Assert.IsType<BarcodeOp>(doc.Ops[1]);
        Assert.Equal(2.5, code128.WideToNarrowRatio);
        Assert.Equal((4, 3), (pdf.Pdf417Security, pdf.Pdf417Columns));
    }

    [Fact]
    public void The_preview_draws_code39_wider_at_ratio_three_than_at_ratio_two()
    {
        static int Width(string zpl)
        {
            using var bmp = SkiaSharp.SKBitmap.Decode(ZplRenderer.Render(zpl).Png);
            var max = 0;
            for (var y = 0; y < bmp.Height; y++)
                for (var x = bmp.Width - 1; x >= 0; x--)
                    if (bmp.GetPixel(x, y).Red < 128) { max = Math.Max(max, x); break; }
            return max;
        }
        var three = Width("^XA^PW600^LL200^BY2,3^FO0,0^B3N,N,60,N,N^FDAB^FS^XZ");
        var two = Width("^XA^PW600^LL200^BY2,2^FO0,0^B3N,N,60,N,N^FDAB^FS^XZ");
        Assert.True(three > two, $"ratio 3 is {three} wide, ratio 2 is {two}");
    }
}
