using System.Text.RegularExpressions;
using SkiaSharp;
using Netcodes.Template;
using Netcodes.Zpl;

namespace Netcodes.Tests;

/// <summary>The Image element: a base64 picture becomes a compressed ^GF field.</summary>
public class ImageElementTests
{
    // 16 dots at 203 dpi expressed in millimetres, so tests can ask for an exact dot width.
    private static double Mm(int dots) => dots * 25.4 / 203;

    private static string Png(int width, int height, Func<int, int, SKColor> pixel)
    {
        using var bmp = new SKBitmap(width, height);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                bmp.SetPixel(x, y, pixel(x, y));
        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return Convert.ToBase64String(data.ToArray());
    }

    // Left half black, right half white.
    private static string HalfBlack => Png(16, 8, (x, _) => x < 8 ? SKColors.Black : SKColors.White);

    private static ZplResult Generate(ImageElement image, params LabelElement[] others) =>
        ZplGenerator.Generate(GeneratorTextBoxTests.Template(others.Append(image).ToArray()), "{}");

    private static ImageOp Read(ZplResult r) => Assert.IsType<ImageOp>(ZplParser.Parse(r.Zpl).Ops.Single());

    [Fact]
    public void A_picture_becomes_a_GF_field_that_the_preview_reads_back()
    {
        var r = Generate(new ImageElement { Data = HalfBlack, WidthMm = Mm(16) });
        Assert.Contains("^GFA,", r.Zpl);
        Assert.Empty(r.Warnings);
        var op = Read(r);
        Assert.Equal((16, 8), (op.WidthDots, op.HeightDots));
        Assert.Equal(0xFF, op.Bits[0]);       // left half black
        Assert.Equal(0x00, op.Bits[1]);       // right half white
    }

    [Fact]
    public void Encode_then_decode_returns_the_same_bits_for_every_shape_of_row()
    {
        var rng = new Random(7);
        // Rows of all zero, all ones, repeats of the row above, runs, and noise, 5 bytes (40 dots) wide.
        var rows = new List<byte[]>
        {
            new byte[5], new byte[] { 255, 255, 255, 255, 255 }, new byte[] { 255, 255, 255, 255, 255 },
            new byte[] { 0xF0, 0xF0, 0xF0, 0, 0 }, new byte[] { 0, 0, 0, 0, 1 },
            Enumerable.Range(0, 5).Select(_ => (byte)rng.Next(256)).ToArray(),
            Enumerable.Range(0, 5).Select(_ => (byte)rng.Next(256)).ToArray(),
            new byte[] { 0, 0, 0, 0xFF, 0xFF },
        };
        var bits = rows.SelectMany(r => r).ToArray();
        // Go through ZPL: our own output must be readable by our own parser (and by printers).
        var zpl = $"^XA^FO0,0^GFA,{bits.Length},{bits.Length},5,{GraphicField.Encode(bits, 5)}^FS^XZ";
        var op = Assert.IsType<ImageOp>(ZplParser.Parse(zpl).Ops.Single());
        Assert.Equal(bits, op.Bits);
    }

    [Fact]
    public void Runs_longer_than_the_largest_repeat_letter_round_trip()
    {
        // 400 identical bytes in a row is 800 nibbles: more than one 'z' (400) can say.
        var bits = Enumerable.Repeat((byte)0xA5, 400).ToArray();
        var zpl = $"^XA^GFA,400,400,400,{GraphicField.Encode(bits, 400)}^FS^XZ";
        Assert.Equal(bits, Assert.IsType<ImageOp>(ZplParser.Parse(zpl).Ops.Single()).Bits);
    }

    [Fact]
    public void Long_runs_are_compressed()
    {
        var r = Generate(new ImageElement { Data = Png(400, 100, (_, _) => SKColors.White), WidthMm = 50 });
        Assert.True(r.Zpl.Length < 2000, $"an all-white picture should be tiny, was {r.Zpl.Length} characters");
    }

    [Fact]
    public void Transparent_pixels_print_white_not_black()
    {
        var png = Png(16, 8, (_, _) => SKColors.Transparent);
        var op = Read(Generate(new ImageElement { Data = png, WidthMm = Mm(16) }));
        Assert.All(op.Bits, b => Assert.Equal(0, b));
    }

    [Fact]
    public void Threshold_and_invert_decide_which_dots_are_black()
    {
        var grey = Png(16, 8, (_, _) => new SKColor(100, 100, 100));
        Assert.Equal(0xFF, Read(Generate(new ImageElement { Data = grey, WidthMm = Mm(16), Threshold = 128 })).Bits[0]);
        Assert.Equal(0x00, Read(Generate(new ImageElement { Data = grey, WidthMm = Mm(16), Threshold = 50 })).Bits[0]);
        Assert.Equal(0x00, Read(Generate(new ImageElement { Data = grey, WidthMm = Mm(16), Threshold = 128, Invert = true })).Bits[0]);
    }

    [Fact]
    public void A_data_uri_and_a_placeholder_both_work()
    {
        var direct = Generate(new ImageElement { Data = "data:image/png;base64," + HalfBlack, WidthMm = Mm(16) });
        Assert.Contains("^GFA,", direct.Zpl);
        var viaData = ZplGenerator.Generate(
            GeneratorTextBoxTests.Template(new ImageElement { Data = "{{logo}}", WidthMm = Mm(16) }),
            $$"""{"logo":"{{HalfBlack}}"}""");
        Assert.Contains("^GFA,", viaData.Zpl);
    }

    [Fact]
    public void Height_follows_the_picture_shape_unless_given()
    {
        var auto = Read(Generate(new ImageElement { Data = HalfBlack, WidthMm = Mm(32) }));
        Assert.Equal((32, 16), (auto.WidthDots, auto.HeightDots));     // 16x8 scaled to 32 wide keeps 2:1
        var given = Read(Generate(new ImageElement { Data = HalfBlack, WidthMm = Mm(32), HeightMm = Mm(24) }));
        Assert.Equal((32, 24), (given.WidthDots, given.HeightDots));
    }

    [Fact]
    public void Rotation_turns_the_picture_before_it_is_made_black_and_white()
    {
        // 16x8 turned 90 degrees is 8 wide and 16 tall; ask for it 8 dots wide.
        var op = Read(Generate(new ImageElement { Data = HalfBlack, WidthMm = Mm(8), Rotation = 90 }));
        Assert.Equal((8, 16), (op.WidthDots, op.HeightDots));
        // The black left half is now the top half.
        Assert.Equal(0xFF, op.Bits[0]);
        Assert.Equal(0x00, op.Bits[15]);
    }

    // Review focus 1: hostile pictures. Each is skipped with a warning and the label still comes out.
    public static IEnumerable<object[]> BadPictures()
    {
        yield return new object[] { "", "no picture" };
        yield return new object[] { "this is not base64!!", "not a picture" };
        yield return new object[] { Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'/>")), "not a picture" };
        yield return new object[] { Convert.ToBase64String(new byte[1000]), "not a picture" };
        yield return new object[] { new string('A', 8_000_000), "too large" };
        yield return new object[] { "C:\\Windows\\win.ini", "not a picture" };
        yield return new object[] { "https://example.test/logo.png", "not a picture" };
    }

    [Theory]
    [MemberData(nameof(BadPictures))]
    public void A_bad_picture_is_skipped_with_a_warning_and_the_label_continues(string data, string mention)
    {
        var r = Generate(new ImageElement { Data = data, WidthMm = 20 }, new TextElement { Text = "keep me", HeightMm = 4 });
        Assert.Contains("keep me", r.Zpl);
        Assert.DoesNotContain("^GF", r.Zpl);
        Assert.Contains(r.Warnings, w => w.Contains("image") && w.Contains(mention, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_picture_that_declares_huge_dimensions_is_refused_before_it_is_decoded()
    {
        // A PNG header that claims 60000 x 60000 pixels (a decompression bomb): only the header should be read.
        var bomb = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52,
            0, 0, 0xEA, 0x60, 0, 0, 0xEA, 0x60, 8, 0, 0, 0, 0, 0, 0, 0, 0,
        };
        var r = Generate(new ImageElement { Data = Convert.ToBase64String(bomb), WidthMm = 20 });
        Assert.DoesNotContain("^GF", r.Zpl);
        Assert.Contains(r.Warnings, w => w.Contains("image"));
    }

    [Fact]
    public void A_valid_picture_with_too_many_pixels_is_refused_by_the_size_check()
    {
        // A real, valid PNG of 6000 x 6000 pixels (36 million, over the 25 million limit) that compresses to a few KB:
        // the refusal must come from the size check on the header, not from the decoder failing.
        using var big = new SKBitmap(new SKImageInfo(6000, 6000, SKColorType.Gray8));
        big.Erase(SKColors.White);
        using var image = SKImage.FromBitmap(big);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        var r = Generate(new ImageElement { Data = Convert.ToBase64String(png.ToArray()), WidthMm = 20 });
        Assert.DoesNotContain("^GF", r.Zpl);
        Assert.Contains(r.Warnings, w => w.Contains("too large") && w.Contains("6000"));
    }

    // Security review: each picture was capped, but a template can hold thousands of pictures. One label gets one budget
    // for decoding work (source pixels) and one for output size.
    [Fact]
    public void Many_large_pictures_share_one_label_wide_budget()
    {
        using var big = new SKBitmap(new SKImageInfo(5000, 5000, SKColorType.Gray8));
        big.Erase(SKColors.White);
        using var image = SKImage.FromBitmap(big);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        var b64 = Convert.ToBase64String(png.ToArray());
        var elements = Enumerable.Range(0, 40)
            .Select(i => (LabelElement)new ImageElement { Data = b64, WidthMm = 10, Y = i })
            .ToArray();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var r = ZplGenerator.Generate(GeneratorTextBoxTests.Template(elements), "{}");
        var written = Regex.Matches(r.Zpl, @"\^GFA,").Count;
        Assert.True(written is > 0 and < 10, $"{written} of 40 huge pictures were decoded");
        Assert.Contains(r.Warnings, w => w.Contains("too many images"));
        Assert.True(watch.ElapsedMilliseconds < 20000, $"took {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void An_output_larger_than_the_limit_is_refused()
    {
        // 300 mm at 600 dpi is about 7000 dots wide: past the 4000-dot cap.
        var t = GeneratorTextBoxTests.Template(new ImageElement { Data = HalfBlack, WidthMm = 300 });
        t.Dpi = 600;
        var r = ZplGenerator.Generate(t, "{}");
        Assert.DoesNotContain("^GF", r.Zpl);
        Assert.Contains(r.Warnings, w => w.Contains("too large"));
    }

    [Fact]
    public void Validation_asks_for_a_width_and_a_sane_threshold()
    {
        var t = GeneratorTextBoxTests.Template(new ImageElement { Data = "x", WidthMm = 0, Threshold = 300 });
        var problems = string.Join(" ", t.Validate());
        Assert.Contains("widthMm", problems);
        Assert.Contains("threshold", problems);
    }

    [Fact]
    public void The_same_logo_in_every_repeat_row_is_written_each_time()
    {
        var repeat = new RepeatElement
        {
            Source = "rows", RowHeightMm = 12,
            Elements = { new ImageElement { Data = HalfBlack, WidthMm = Mm(16) } },
        };
        var r = ZplGenerator.Generate(GeneratorTextBoxTests.Template(repeat), """{"rows":[1,2,3]}""");
        Assert.Equal(3, Regex.Matches(r.Zpl, @"\^GFA,").Count);
    }

    [Fact]
    public void Image_elements_survive_template_json()
    {
        var json = LabelTemplate.FromJson(
            """{"name":"x","widthMm":50,"heightMm":30,"elements":[{"type":"image","data":"abc","widthMm":20,"threshold":90,"invert":true}]}""").ToJson();
        Assert.Contains("\"type\": \"image\"", json);
        Assert.Contains("90", json);
    }
}
