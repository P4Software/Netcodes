using SkiaSharp;
using Netcodes.Zpl;

namespace Netcodes.Tests;

/// <summary>Reading ^GF graphics (ASCII hex with Zebra's run-length shorthand) and drawing them.</summary>
public class GraphicFieldTests
{
    private static ImageOp Parse(string zpl) => Assert.IsType<ImageOp>(ZplParser.Parse(zpl).Ops[0]);

    [Fact]
    public void Plain_hex_is_read_row_by_row()
    {
        // 2 bytes per row, 2 rows: FF00 / 00FF.
        var op = Parse("^XA^FO10,20^GFA,4,4,2,FF0000FF^FS^XZ");
        Assert.Equal((10, 20, 16, 2, 2), (op.X, op.Y, op.WidthDots, op.HeightDots, op.BytesPerRow));
        Assert.Equal(new byte[] { 0xFF, 0x00, 0x00, 0xFF }, op.Bits);
    }

    [Fact]
    public void Compression_letters_commas_bangs_and_colons_are_expanded()
    {
        // Two bytes (four nibbles) per row, four rows:
        // Row 1 = "FF" then "," (rest of the row is zero)  -> FF 00
        // Row 2 = ":"   (same as the row above)              -> FF 00
        // Row 3 = "!"   (whole row ones)                     -> FF FF
        // Row 4 = "I0" (three zero nibbles) then "F"         -> 00 0F
        var op = Parse("^XA^GFA,8,8,2,FF,:!I0F^FS^XZ");
        Assert.Equal(new byte[] { 0xFF, 0x00, 0xFF, 0x00, 0xFF, 0xFF, 0x00, 0x0F }, op.Bits);
    }

    [Fact]
    public void Baseline_origin_moves_the_graphic_up_by_its_height_in_the_preview()
    {
        // 8x8 solid block at ^FT y=20: the bottom-left corner is the origin, so ink covers rows 12..19.
        var r = ZplRenderer.Render("^XA^PW40^LL40^FT10,20^GFA,8,8,1,FFFFFFFFFFFFFFFF^FS^XZ");
        using var bmp = SKBitmap.Decode(r.Png);
        Assert.True(bmp.GetPixel(12, 12).Red < 128);
        Assert.True(bmp.GetPixel(12, 19).Red < 128);
        Assert.True(bmp.GetPixel(12, 20).Red > 128);
        Assert.True(bmp.GetPixel(12, 11).Red > 128);
    }

    [Fact]
    public void The_preview_draws_set_bits_black_and_clear_bits_white()
    {
        // One byte per row, two rows: 10101010 / 01010101.
        var r = ZplRenderer.Render("^XA^PW20^LL20^FO0,0^GFA,2,2,1,AA55^FS^XZ");
        using var bmp = SKBitmap.Decode(r.Png);
        Assert.True(bmp.GetPixel(0, 0).Red < 128);
        Assert.True(bmp.GetPixel(1, 0).Red > 128);
        Assert.True(bmp.GetPixel(0, 1).Red > 128);
        Assert.True(bmp.GetPixel(1, 1).Red < 128);
    }

    // Review focus 2: hostile graphics.
    [Fact]
    public void A_declared_size_of_a_billion_bytes_does_not_allocate_it()
    {
        var doc = ZplParser.Parse("^XA^GFA,1000000000,1000000000,100,FF^FS^XZ");
        Assert.Empty(doc.Ops.OfType<ImageOp>());
        Assert.Contains(doc.Warnings, w => w.Contains("graphic"));
    }

    [Fact]
    public void A_repeat_flood_is_capped_and_fast()
    {
        // 'z' repeats the next nibble 400 times; 200,000 of them would be 80 million nibbles.
        var zpl = "^XA^PW100^LL100^GFA,100,100,10," + string.Concat(Enumerable.Repeat("zF", 200_000)) + "^FS^XZ";
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var r = ZplRenderer.Render(zpl);
        Assert.True(watch.ElapsedMilliseconds < 5000, $"took {watch.ElapsedMilliseconds} ms");
        Assert.NotEmpty(r.Png);
    }

    // Security review of Task 5: each graphic was capped, but 5000 fields that each declare 2 MB (and cost 30 bytes of
    // ZPL) would still ask for 10 GB. The whole label gets one graphics budget.
    [Fact]
    public void Many_declared_graphics_share_one_budget()
    {
        var field = "^FO0,0^GFA,2000000,2000000,500,FF^FS";
        var zpl = "^XA^PW800^LL800" + string.Concat(Enumerable.Repeat(field, 3000)) + "^XZ";
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var doc = ZplParser.Parse(zpl);
        var total = doc.Ops.OfType<ImageOp>().Sum(o => (long)o.Bits.Length);
        Assert.True(total <= GraphicField.MaxTotalBytes, $"{total:N0} bytes of graphics were allocated");
        Assert.Contains(doc.Warnings, w => w.Contains("graphics"));
        var r = ZplRenderer.Render(zpl);
        Assert.NotEmpty(r.Png);
        Assert.True(watch.ElapsedMilliseconds < 10000, $"took {watch.ElapsedMilliseconds} ms");
    }

    [Theory]
    [InlineData("^XA^GFA,4,4,0,FF0000FF^FS^XZ")]
    [InlineData("^XA^GFA,4,4,-3,FF0000FF^FS^XZ")]
    [InlineData("^XA^GFA,4,4,5000,FF0000FF^FS^XZ")]
    [InlineData("^XA^GFA,abc,def,2,FF^FS^XZ")]
    [InlineData("^XA^GFB,4,4,2,FF0000FF^FS^XZ")]
    [InlineData("^XA^GFA,4,4,2,^FS^XZ")]
    public void Unusable_graphics_give_a_warning_not_a_crash(string zpl)
    {
        var doc = ZplParser.Parse(zpl);
        Assert.Empty(doc.Ops.OfType<ImageOp>());
        Assert.Contains(doc.Warnings, w => w.Contains("graphic"));
        Assert.NotEmpty(ZplRenderer.Render(zpl).Png);
    }

    [Fact]
    public void Truncated_data_is_padded_with_blank_rows()
    {
        // Declares 2 rows of 2 bytes but supplies only the first row.
        var op = Parse("^XA^GFA,4,4,2,FFFF^FS^XZ");
        Assert.Equal(2, op.HeightDots);
        Assert.Equal(new byte[] { 0xFF, 0xFF, 0x00, 0x00 }, op.Bits);
    }

    [Fact]
    public void Reverse_printing_flips_the_pixels_underneath()
    {
        // A black box with a reversed solid graphic on top comes out white inside it.
        var r = ZplRenderer.Render("^XA^PW30^LL30^FO0,0^GB30,30,30^FS^FO5,5^FR^GFA,8,8,1,FFFFFFFFFFFFFFFF^FS^XZ");
        using var bmp = SKBitmap.Decode(r.Png);
        Assert.True(bmp.GetPixel(8, 8).Red > 128);
        Assert.True(bmp.GetPixel(1, 1).Red < 128);
    }
}
