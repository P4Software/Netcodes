using System.Text.RegularExpressions;
using SkiaSharp;
using Netcodes.Template;
using Netcodes.Zpl;

namespace Netcodes.Tests;

public class Pdf417Tests
{
    [Fact]
    public void The_encoder_returns_logical_rows_not_zxing_repeated_rows()
    {
        var m = new ZXingBarcodeEncoder().Encode(ZplSymbology.Pdf417, "HELLO PDF417");
        Assert.InRange(m.Height, 3, 90);         // PDF417 has 3 to 90 rows
        Assert.True(m.Width >= 86);              // start + left + one data column + right + stop = 17+17+17+17+18
        // No two neighbouring rows are the same, otherwise the collapse did not happen.
        for (var y = 1; y < m.Height; y++)
        {
            var same = true;
            for (var x = 0; x < m.Width && same; x++) same = m[x, y] == m[x, y - 1];
            Assert.False(same, $"rows {y - 1} and {y} are identical");
        }
    }

    [Fact]
    public void The_generator_writes_B7_with_a_row_height_from_the_overall_height()
    {
        var r = ZplGenerator.Generate(GeneratorTextBoxTests.Template(
            new BarcodeElement { Symbology = Symbology.Pdf417, Data = "HELLO PDF417", HeightMm = 20, ModuleWidth = 2 }), "{}");
        Assert.Matches(@"\^BY2,3,\d+\^FO\d+,\d+\^B7N,\d+,0,\d+,,N\^FDHELLO PDF417\^FS", r.Zpl);
        var rowHeight = int.Parse(Regex.Match(r.Zpl, @"\^B7N,(\d+)").Groups[1].Value);
        Assert.True(rowHeight >= 6, "a row is never thinner than 3 modules");
    }

    [Fact]
    public void The_parser_reads_B7_row_height()
    {
        var op = Assert.IsType<BarcodeOp>(ZplParser.Parse("^XA^BY2^FO10,10^B7N,8,0,,,N^FDHELLO^FS^XZ").Ops[0]);
        Assert.Equal((ZplSymbology.Pdf417, 8, 2), (op.Symbology, op.HeightDots, op.ModuleWidth));
    }

    [Fact]
    public void The_preview_draws_PDF417_with_the_row_height()
    {
        var thin = ZplRenderer.Render("^XA^PW600^LL300^BY2^FO10,10^B7N,4,0,,,N^FDHELLO PDF417^FS^XZ");
        var tall = ZplRenderer.Render("^XA^PW600^LL300^BY2^FO10,10^B7N,12,0,,,N^FDHELLO PDF417^FS^XZ");
        Assert.Empty(thin.Warnings);
        static int LastInkRow(byte[] png)
        {
            using var bmp = SKBitmap.Decode(png);
            for (var y = bmp.Height - 1; y >= 0; y--)
                for (var x = 0; x < bmp.Width; x++)
                    if (bmp.GetPixel(x, y).Red < 128) return y;
            return -1;
        }
        Assert.True(LastInkRow(tall.Png) > LastInkRow(thin.Png));
    }

    [Fact]
    public void Data_that_is_too_long_is_skipped_with_a_warning()
    {
        var r = ZplGenerator.Generate(GeneratorTextBoxTests.Template(
            new TextElement { Text = "keep me", HeightMm = 4 },
            new BarcodeElement { Symbology = Symbology.Pdf417, Data = new string('A', 5000), HeightMm = 20 }), "{}");
        Assert.Contains("keep me", r.Zpl);
        Assert.DoesNotContain("^B7", r.Zpl);
        Assert.Contains(r.Warnings, w => w.Contains("PDF417"));
    }
}
