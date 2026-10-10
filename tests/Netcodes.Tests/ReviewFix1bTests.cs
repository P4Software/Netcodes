using System.Text.RegularExpressions;
using SkiaSharp;
using Netcodes.Template;
using Netcodes.Zpl;

namespace Netcodes.Tests;

/// <summary>Findings of the Plan 1b whole-branch review.</summary>
public class ReviewFix1bTests
{
    private static ZplResult Generate(GenerateOptions options) =>
        ZplGenerator.Generate(GeneratorTextBoxTests.Template(), "{}", options);

    // Important 1: a number that is not a real enum value must give a plain message, not IndexOutOfRangeException.
    [Fact]
    public void Undefined_option_values_are_refused_in_plain_words()
    {
        Assert.Throws<TemplateException>(() => Generate(new GenerateOptions { Mode = (PrintMode)6 }));
        Assert.Throws<TemplateException>(() => Generate(new GenerateOptions { Tracking = (MediaTracking)9 }));
        Assert.Throws<TemplateException>(() => Generate(new GenerateOptions { Media = (PrinterMedia)5 }));
        Assert.Throws<TemplateException>(() => Generate(new GenerateOptions { ConfigUpdate = (ConfigUpdate)7 }));
    }

    // Important 1 (related, older): an undefined symbology number deserializes and then threw NotSupportedException.
    [Fact]
    public void An_undefined_symbology_is_refused_in_plain_words()
    {
        var t = LabelTemplate.FromJson(
            """{"name":"x","widthMm":50,"heightMm":30,"elements":[{"type":"barcode","symbology":99,"data":"1"}]}""");
        var ex = Assert.Throws<TemplateException>(() => ZplGenerator.Generate(t, "{}"));
        Assert.Contains("symbology", ex.Message);
    }

    // Important 2: ^JUR reloads the saved configuration, so it must come BEFORE the settings of this label.
    [Fact]
    public void Recall_is_written_first_and_save_last()
    {
        var recall = Generate(new GenerateOptions { PrintSpeed = 4, Darkness = 20, ConfigUpdate = ConfigUpdate.Recall }).Zpl;
        Assert.StartsWith("^XA\n^JUR\n", recall);
        var save = Generate(new GenerateOptions { PrintSpeed = 4, ConfigUpdate = ConfigUpdate.Save }).Zpl;
        Assert.True(save.IndexOf("^JUS", StringComparison.Ordinal) > save.IndexOf("^PR4", StringComparison.Ordinal));
    }

    // Important 3: a Codabar with its own start and stop letters must print the same as it previews.
    [Fact]
    public void A_framed_codabar_puts_its_letters_in_the_start_and_stop_parameters()
    {
        var r = ZplGenerator.Generate(GeneratorTextBoxTests.Template(
            new BarcodeElement { Symbology = Symbology.Codabar, Data = "B1234D", HeightMm = 15 }), "{}");
        Assert.Contains("^BKN,N,120,Y,N,B,D^FD1234^FS", r.Zpl);
    }

    [Fact]
    public void The_preview_uses_the_start_and_stop_letters_of_the_command()
    {
        var framed = ZplParser.Parse("^XA^BY2^FO0,0^BKN,N,60,Y,N,B,D^FD123^FS^XZ");
        Assert.Equal("B123D", Assert.IsType<BarcodeOp>(framed.Ops[0]).Data);
        var plain = ZplParser.Parse("^XA^BY2^FO0,0^BKN,N,60,Y,N,A,A^FD123^FS^XZ");
        Assert.Equal("A123A", Assert.IsType<BarcodeOp>(plain.Ops[0]).Data);
        var defaults = ZplParser.Parse("^XA^BY2^FO0,0^BKN,N,60,Y,N^FD123^FS^XZ");
        Assert.Equal("A123A", Assert.IsType<BarcodeOp>(defaults.Ops[0]).Data);
    }

    // Important 4: the size of the picture TEXT must be charged too (a 1x1 picture followed by megabytes of junk).
    [Fact]
    public void Many_pictures_with_a_large_source_text_share_one_budget()
    {
        using var bmp = new SKBitmap(1, 1);
        using var image = SKImage.FromBitmap(bmp);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = png.ToArray().Concat(new byte[5_000_000]).ToArray();      // junk after the picture is ignored by the reader
        var big = Convert.ToBase64String(bytes);
        var elements = Enumerable.Range(0, 100)
            .Select(i => (LabelElement)new ImageElement { Data = big, WidthMm = 5, Y = i })
            .ToArray();
        var r = ZplGenerator.Generate(GeneratorTextBoxTests.Template(elements), "{}");
        var written = Regex.Matches(r.Zpl, @"\^GFA,").Count;
        Assert.True(written < 10, $"{written} of 100 pictures were decoded");
        Assert.Contains(r.Warnings, w => w.Contains("too many images"));
    }

    // Important 6: write the column count the preview chose, so the printed symbol has the same shape.
    [Fact]
    public void Pdf417_is_written_with_an_explicit_column_count()
    {
        var r = ZplGenerator.Generate(GeneratorTextBoxTests.Template(
            new BarcodeElement { Symbology = Symbology.Pdf417, Data = "Netcodes PDF417 test 12345", HeightMm = 20 }), "{}");
        var columns = int.Parse(Regex.Match(r.Zpl, @"\^B7N,\d+,0,(\d+),,N").Groups[1].Value);
        var width = new ZXingBarcodeEncoder().Encode(ZplSymbology.Pdf417, "Netcodes PDF417 test 12345").Width;
        Assert.Equal((width - 69) / 17, columns);
        // And the preview reading that ZPL back gets the same shape.
        var op = Assert.IsType<BarcodeOp>(ZplParser.Parse(r.Zpl).Ops.Single());
        Assert.Equal(columns, op.Pdf417Columns);
    }
}
