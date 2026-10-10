using SkiaSharp;
using Netcodes.Zpl;

namespace Netcodes.Tests;

public class RendererTests
{
    private static SKBitmap Draw(string zpl, out RenderResult result)
    {
        result = ZplRenderer.Render(zpl);
        return SKBitmap.Decode(result.Png);
    }

    private static bool Black(SKBitmap b, int x, int y) => b.GetPixel(x, y).Red < 128;

    private static (int MinX, int MinY, int MaxX, int MaxY)? BlackBounds(SKBitmap b)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (var y = 0; y < b.Height; y++)
            for (var x = 0; x < b.Width; x++)
                if (Black(b, x, y))
                {
                    minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                }
        return maxX < 0 ? null : (minX, minY, maxX, maxY);
    }

    [Fact]
    public void Renders_a_white_label_of_the_requested_size()
    {
        using var bmp = Draw("^XA^PW400^LL300^XZ", out var r);
        Assert.Equal((400, 300), (bmp.Width, bmp.Height));
        Assert.Equal((400, 300), (r.WidthDots, r.HeightDots));
        Assert.Null(BlackBounds(bmp));
    }

    [Fact]
    public void Missing_size_uses_the_4x6_inch_default()
    {
        using var bmp = Draw("^XA^FO0,0^FDx^FS^XZ", out _);
        Assert.Equal((812, 1218), (bmp.Width, bmp.Height));
    }

    [Fact]
    public void Box_border_is_drawn_and_the_inside_stays_white()
    {
        using var bmp = Draw("^XA^PW812^LL1218^FO50,200^GB300,100,4^FS^XZ", out _);
        Assert.True(Black(bmp, 50, 250));      // left border
        Assert.True(Black(bmp, 200, 201));     // top border
        Assert.True(Black(bmp, 348, 250));     // right border
        Assert.False(Black(bmp, 200, 250));    // inside
    }

    [Fact]
    public void Reverse_print_inverts_over_a_black_area()
    {
        using var bmp = Draw("^XA^FO100,100^GB100,100,100^FS^FO120,120^GB40,40,40^FR^FS^XZ", out _);
        Assert.True(Black(bmp, 110, 110));
        Assert.False(Black(bmp, 140, 140));
    }

    [Fact]
    public void Code128_bars_start_where_the_start_pattern_says()
    {
        using var bmp = Draw("^XA^FO50,400^BY2^BCN,100,N,N,N^FD123456^FS^XZ", out var r);
        // Start C is 11010011100: modules 0-1 dark, module 2 light; each module is 2 dots wide.
        Assert.True(Black(bmp, 50, 450));
        Assert.True(Black(bmp, 53, 450));
        Assert.False(Black(bmp, 54, 450));
        Assert.False(Black(bmp, 49, 450));
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void Qr_finder_pattern_is_drawn_at_the_magnification()
    {
        using var bmp = Draw("^XA^FO50,600^BQN,2,5^FDQA,HELLO^FS^XZ", out _);
        Assert.True(Black(bmp, 52, 602));      // module (0,0) dark
        Assert.False(Black(bmp, 57, 607));     // module (1,1) light ring
        Assert.True(Black(bmp, 67, 617));      // module (3,3) dark centre
    }

    [Fact]
    public void Text_draws_black_pixels_near_its_position()
    {
        using var bmp = Draw("^XA^FO100,100^A0N,40,40^FDHELLO^FS^XZ", out _);
        var b = BlackBounds(bmp)!.Value;
        Assert.InRange(b.MinX, 98, 110);
        Assert.InRange(b.MinY, 98, 115);
        Assert.True(b.MaxX - b.MinX > b.MaxY - b.MinY);
    }

    [Fact]
    public void Rotated_text_runs_downwards_instead_of_across()
    {
        using var bmp = Draw("^XA^FO100,100^A0R,40,40^FDHELLO^FS^XZ", out _);
        var b = BlackBounds(bmp)!.Value;
        Assert.True(b.MaxY - b.MinY > b.MaxX - b.MinX);
        Assert.InRange(b.MinX, 98, 140);
    }

    [Fact]
    public void Invalid_barcode_data_warns_and_the_rest_of_the_label_still_draws()
    {
        using var bmp = Draw("^XA^FO10,10^BEN,50,Y,N^FDabc^FS^FO10,100^GB50,50,50^FS^XZ", out var r);
        Assert.Contains(r.Warnings, w => w.Contains("EAN-13"));
        Assert.True(Black(bmp, 30, 120));
    }

    [Fact]
    public void Unsupported_commands_are_reported_in_the_result()
    {
        Draw("^XA^ZZ9^FO0,0^FDok^FS^XZ", out var r);
        Assert.Contains(r.Warnings, w => w.Contains("^ZZ"));
    }

    [Fact]
    public void An_absurd_label_size_falls_back_to_the_default_with_a_warning()
    {
        using var bmp = Draw("^XA^PW99999^LL99999^XZ", out var r);
        Assert.Equal((812, 1218), (bmp.Width, bmp.Height));
        Assert.Contains(r.Warnings, w => w.Contains("outside what the preview can draw"));
    }

    [Fact]
    public void Garbage_input_never_throws()
    {
        var r = ZplRenderer.Render("this is not zpl ^^^ ~~ ^FO,^FD");
        Assert.NotEmpty(r.Png);
    }
}
