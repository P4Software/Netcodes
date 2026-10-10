using SkiaSharp;
using Netcodes.Zpl;

namespace Netcodes.Tests;

/// <summary>
/// Where rotated fields land. The numbers come from comparing our preview with Labelary's for the same ZPL
/// (fixture rotated.zpl): ^FT puts the START of the text on the baseline at the given point and the text then runs
/// in its own direction, so for each rotation the field sits on a different side of that point.
/// </summary>
public class RotatedFieldTests
{
    private static (int MinX, int MaxX, int MinY, int MaxY) Ink(string zpl)
    {
        using var bmp = SKBitmap.Decode(ZplRenderer.Render("^XA^PW812^LL1218" + zpl + "^XZ").Png);
        int minX = int.MaxValue, maxX = -1, minY = int.MaxValue, maxY = -1;
        for (var y = 0; y < bmp.Height; y++)
            for (var x = 0; x < bmp.Width; x++)
                if (bmp.GetPixel(x, y).Red < 128)
                {
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                }
        return (minX, maxX, minY, maxY);
    }

    [Fact]
    public void FT_normal_text_sits_on_the_baseline_at_the_point()
    {
        var ink = Ink("^FT50,500^A0N,40,40^FDFT normal^FS");
        Assert.InRange(ink.MaxY, 496, 510);       // the baseline is at 500 (descenders may reach a little lower)
        Assert.InRange(ink.MinX, 48, 56);
    }

    [Fact]
    public void FT_rotated_90_text_runs_down_from_the_point_with_its_tops_to_the_right()
    {
        var ink = Ink("^FT400,450^A0R,40,40^FDFT R^FS");
        Assert.InRange(ink.MinX, 396, 404);
        Assert.InRange(ink.MaxX, 425, 446);
        Assert.InRange(ink.MinY, 446, 456);
    }

    [Fact]
    public void FT_rotated_180_text_runs_left_from_the_point_with_its_tops_down()
    {
        var ink = Ink("^FT750,600^A0I,40,40^FDFT I^FS");
        Assert.InRange(ink.MaxX, 740, 754);       // within a glyph side bearing of the point
        Assert.InRange(ink.MinY, 596, 604);
        Assert.InRange(ink.MaxY, 625, 650);
    }

    [Fact]
    public void FT_rotated_270_text_runs_up_from_the_point_with_its_tops_to_the_left()
    {
        var ink = Ink("^FT100,700^A0B,40,40^FDFT B^FS");
        Assert.InRange(ink.MaxX, 96, 104);
        Assert.InRange(ink.MinX, 55, 76);
        Assert.InRange(ink.MaxY, 690, 704);       // within a glyph side bearing of the point
    }

    [Fact]
    public void FO_rotated_90_barcode_has_its_bars_at_the_origin_and_the_caption_outside_to_the_left()
    {
        // Labelary: bars x 400..460 (60 dots high), the interpretation line to the left of x=400.
        var ink = Ink("^BY2^FO400,800^BCR,60,Y,N,N^FD123456^FS");
        Assert.InRange(ink.MaxX, 457, 463);
        Assert.True(ink.MinX < 400, "the caption sits outside the bars, to the left");
        Assert.InRange(ink.MinY, 798, 806);
    }

    [Fact]
    public void FT_rotated_90_barcode_starts_at_the_point_like_FO()
    {
        var ink = Ink("^BY2^FT400,1100^BCR,60,Y,N,N^FD123456^FS");
        Assert.InRange(ink.MaxX, 457, 463);
        Assert.InRange(ink.MinY, 1096, 1106);
    }
}
