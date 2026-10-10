using Netcodes.Template;
using Netcodes.Zpl;

namespace Netcodes.Tests;

public class PrinterControlTests
{
    [Fact]
    public void Control_commands_are_written_only_when_requested()
    {
        var plain = ZplGenerator.Generate(GeneratorTextBoxTests.Template(), "{}").Zpl;
        Assert.DoesNotContain("^PR", plain);
        Assert.DoesNotContain("^MD", plain);
        Assert.DoesNotContain("^MT", plain);
        Assert.DoesNotContain("^PQ", plain);
    }

    [Fact]
    public void Speed_darkness_media_and_quantity_are_written_in_the_right_places()
    {
        var zpl = ZplGenerator.Generate(GeneratorTextBoxTests.Template(), "{}",
            new GenerateOptions { PrintSpeed = 4, Darkness = 20, Media = PrinterMedia.Thermal, Quantity = 3 }).Zpl;
        Assert.Contains("^LH0,0\n^PR4\n^MD20\n^MTT\n", zpl);
        Assert.EndsWith("^PQ3\n^XZ\n", zpl);
    }

    [Fact]
    public void Direct_thermal_media_is_selected_with_D()
    {
        var zpl = ZplGenerator.Generate(GeneratorTextBoxTests.Template(), "{}",
            new GenerateOptions { Media = PrinterMedia.Direct }).Zpl;
        Assert.Contains("^MTD\n", zpl);
    }

    [Theory]
    [InlineData(0, null, null, "speed")]
    [InlineData(15, null, null, "speed")]
    [InlineData(null, -1, null, "darkness")]
    [InlineData(null, 31, null, "darkness")]
    [InlineData(null, null, 0, "quantity")]
    [InlineData(null, null, 10001, "quantity")]
    public void Out_of_range_values_are_refused_in_plain_language(int? speed, int? darkness, int? quantity, string mention)
    {
        var options = new GenerateOptions { PrintSpeed = speed, Darkness = darkness, Quantity = quantity };
        var ex = Assert.Throws<TemplateException>(() => ZplGenerator.Generate(GeneratorTextBoxTests.Template(), "{}", options));
        Assert.Contains(mention, ex.Message);
    }
}
