using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using SkiaSharp;
using Xunit.Abstractions;
using Netcodes.Zpl;

namespace Netcodes.Tests;

/// <summary>
/// Compares our preview with Labelary's for each fixture. Run on purpose with:
/// <c>dotnet test --filter Category=Labelary</c> (needs internet). Excluded from the normal run:
/// <c>dotnet test --filter Category!=Labelary</c>.
/// </summary>
[Trait("Category", "Labelary")]
public class LabelaryFidelityTests
{
    private readonly ITestOutputHelper _output;

    public LabelaryFidelityTests(ITestOutputHelper output) => _output = output;

    public static IEnumerable<object[]> Fixtures() =>
        Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures"), "*.zpl")
            .Select(f => new object[] { Path.GetFileName(f) });

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task Matches_labelary_within_the_recorded_tolerance(string fixture)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture);
        var zpl = await File.ReadAllTextAsync(path);
        var allowed = double.Parse(
            (await File.ReadAllTextAsync(Path.ChangeExtension(path, ".max.txt"))).Trim(), CultureInfo.InvariantCulture);

        using var http = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.labelary.com/v1/printers/8dpmm/labels/4x6/0/")
        {
            Content = new StringContent(zpl, Encoding.UTF8, "application/x-www-form-urlencoded"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/png"));
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var reference = SKBitmap.Decode(await response.Content.ReadAsByteArrayAsync());
        await Task.Delay(500);

        var ours = SKBitmap.Decode(ZplRenderer.Render(zpl).Png);
        Assert.Equal((reference.Width, reference.Height), (ours.Width, ours.Height));

        var different = 0;
        for (var y = 0; y < ours.Height; y++)
            for (var x = 0; x < ours.Width; x++)
                if ((ours.GetPixel(x, y).Red < 128) != (reference.GetPixel(x, y).Red < 128))
                    different++;
        var ratio = different / (double)(ours.Width * ours.Height);

        WriteDiffImage(fixture, ours, reference);

        _output.WriteLine($"{fixture}: {ratio:F5} of pixels differ (allowed {allowed:F5})");
        Assert.True(ratio <= allowed, $"{fixture} differs from Labelary in {ratio:P2} of its pixels (allowed {allowed:P2}).");
    }

    // Why: a percentage says how much differs, not where. The image shows ours (left), Labelary's (middle) and the
    // difference (right: red = only ours has ink, blue = only Labelary's has ink). It goes next to the test binaries.
    private static void WriteDiffImage(string fixture, SKBitmap ours, SKBitmap reference)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "LabelaryDiff");
        Directory.CreateDirectory(dir);
        using var diff = new SKBitmap(ours.Width * 3, ours.Height);
        using var canvas = new SKCanvas(diff);
        canvas.Clear(SKColors.White);
        using (var left = SKImage.FromBitmap(ours)) canvas.DrawImage(left, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest), null);
        using (var middle = SKImage.FromBitmap(reference)) canvas.DrawImage(middle, ours.Width, 0, new SKSamplingOptions(SKFilterMode.Nearest), null);
        for (var y = 0; y < ours.Height; y++)
            for (var x = 0; x < ours.Width; x++)
            {
                var a = ours.GetPixel(x, y).Red < 128;
                var b = reference.GetPixel(x, y).Red < 128;
                diff.SetPixel(ours.Width * 2 + x, y,
                    a == b ? (a ? new SKColor(200, 200, 200) : SKColors.White) : (a ? SKColors.Red : SKColors.Blue));
            }
        using var image = SKImage.FromBitmap(diff);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(dir, fixture + ".png"), data.ToArray());
    }
}
