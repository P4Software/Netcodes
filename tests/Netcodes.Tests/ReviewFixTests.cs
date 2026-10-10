using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using SkiaSharp;
using Netcodes.Printing;
using Netcodes.Template;
using Netcodes.Zpl;

namespace Netcodes.Tests;

/// <summary>Tests for the findings of the final whole-branch review.</summary>
public class ReviewFixTests
{
    private static string Zpl(params LabelElement[] elements) =>
        ZplGenerator.Generate(GeneratorTextBoxTests.Template(elements), "{}").Zpl;

    // C1: a thin filled block must stay a thin line, not become a square.
    [Fact]
    public void A_thin_filled_box_stays_a_line()
    {
        var zpl = Zpl(new BoxElement { WidthMm = 80, HeightMm = 0.5, Filled = true });
        Assert.Contains("^GB639,4,4^FS", zpl);
    }

    // I1: one bad GS1 value drops that barcode with a warning instead of aborting the label.
    [Fact]
    public void A_missing_gs1_value_skips_that_barcode_and_keeps_the_label()
    {
        var gs1 = new Gs1Element { Fields = new() { ["sscc"] = "{{sscc}}" } };
        var r = ZplGenerator.Generate(
            GeneratorTextBoxTests.Template(new TextElement { Text = "keep me", HeightMm = 4 }, gs1), "{}");
        Assert.Contains("keep me", r.Zpl);
        Assert.DoesNotContain("^BC", r.Zpl);
        Assert.Contains(r.Warnings, w => w.Contains("GS1"));
    }

    // I2: explicit JSON nulls must give a plain message or a label, never a NullReferenceException.
    [Theory]
    [InlineData("""{"name":"x","widthMm":50,"heightMm":30,"elements":null}""")]
    [InlineData("""{"name":"x","widthMm":50,"heightMm":30,"elements":[null]}""")]
    [InlineData("""{"name":"x","widthMm":50,"heightMm":30,"elements":[{"type":"text","text":null}]}""")]
    [InlineData("""{"name":"x","widthMm":50,"heightMm":30,"elements":[{"type":"text","text":"a","font":null}]}""")]
    [InlineData("""{"name":"x","widthMm":50,"heightMm":30,"elements":[{"type":"gs1","fields":null}]}""")]
    [InlineData("""{"name":"x","widthMm":50,"heightMm":30,"elements":[{"type":"gs1","fields":{"lot":null}}]}""")]
    [InlineData("""{"name":"x","widthMm":50,"heightMm":30,"elements":[{"type":"barcode","data":null}]}""")]
    [InlineData("""{"name":"x","widthMm":50,"heightMm":30,"elements":[{"type":"repeat","source":null,"rowHeightMm":5}]}""")]
    public void Json_nulls_never_crash(string json)
    {
        var ex = Record.Exception(() => ZplGenerator.Generate(LabelTemplate.FromJson(json), "{}"));
        Assert.True(ex is null or TemplateException, ex?.ToString());
    }

    // I3: automatic mode, so ">" in the data is printed literally and digits are packed like the preview does.
    [Fact]
    public void Code128_uses_automatic_mode_so_a_greater_than_sign_is_literal()
    {
        var zpl = Zpl(new BarcodeElement { Data = "SKU>5123", HeightMm = 15 });
        Assert.Contains("^BCN,120,Y,N,N,A^FDSKU>5123^FS", zpl);
    }

    // I4: ZPL prints overflow text on top of the last line; the preview must show that, not hide it.
    [Fact]
    public void Overflowing_block_text_is_overprinted_on_the_last_line_in_the_preview()
    {
        static int Ink(string zpl)
        {
            using var bmp = SKBitmap.Decode(ZplRenderer.Render(zpl).Png);
            var n = 0;
            for (var y = 0; y < bmp.Height; y++)
                for (var x = 0; x < bmp.Width; x++)
                    if (bmp.GetPixel(x, y).Red < 128) n++;
            return n;
        }
        var first = Ink("^XA^PW400^LL300^FO10,10^FB60,1,0,L,0^A0N,30,30^FDAAAA^FS^XZ");
        var overflow = Ink("^XA^PW400^LL300^FO10,10^FB60,1,0,L,0^A0N,30,30^FDAAAA BBBB CCCC^FS^XZ");
        Assert.True(overflow > first, $"overflow ink {overflow} should exceed first-line ink {first}");
    }

    // I5: a huge wrapped text must not tie up the server.
    [Fact]
    public void A_huge_text_field_is_cut_instead_of_wrapping_for_minutes()
    {
        var zpl = "^XA^PW400^LL300^FO0,0^FB0,1^A0N,20,20^FD" + string.Concat(Enumerable.Repeat("a ", 450_000)) + "^FS^XZ";
        var watch = Stopwatch.StartNew();
        var r = ZplRenderer.Render(zpl);
        Assert.True(watch.ElapsedMilliseconds < 5000, $"took {watch.ElapsedMilliseconds} ms");
        Assert.Contains(r.Warnings, w => w.Contains("too long"));
    }

    // I6 (IOException on a reset connection, write timeout) is fixed in ZplPrinter but has no automated test: a
    // loopback socket reset could not be provoked reliably, and a flaky test is worse than none.

    // I8: a dry run must refuse what a real print would refuse.
    [Fact]
    public async Task A_dry_run_for_a_printer_without_an_address_is_refused()
    {
        var r = await new ZplPrinter().PrintAsync(new PrinterTarget("Dock 1"), "^XA^XZ");
        Assert.False(r.Success);
        Assert.Contains("no address", r.Message);
    }

    // I9: invalid barcode data is reported when generating, not only when previewing.
    [Theory]
    [InlineData(Symbology.Ean13, "ABC", "EAN-13", "^BE")]
    [InlineData(Symbology.Code39, "abc", "Code 39", "^B3")]
    [InlineData(Symbology.Code128, "Müller", "Code 128", "^BC")]
    [InlineData(Symbology.Ean13, "6291041500210", "EAN-13", "^BE")]
    public void Invalid_barcode_data_is_reported_by_the_generator(Symbology symbology, string data, string mention, string command)
    {
        var r = ZplGenerator.Generate(
            GeneratorTextBoxTests.Template(new BarcodeElement { Symbology = symbology, Data = data, HeightMm = 15 }), "{}");
        Assert.DoesNotContain(command, r.Zpl);
        Assert.Contains(r.Warnings, w => w.Contains(mention));
    }
}
