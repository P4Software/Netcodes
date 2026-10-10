using Netcodes.Template;
using Netcodes.Zpl;

namespace Netcodes.Tests;

/// <summary>The printer-control commands added in Plan 1b: ^LS ^MM ^MN ^PO ^JU.</summary>
public class PrinterControlExtraTests
{
    private static string Generate(GenerateOptions options) =>
        ZplGenerator.Generate(GeneratorTextBoxTests.Template(), "{}", options).Zpl;

    [Fact]
    public void Nothing_extra_is_written_unless_requested()
    {
        var zpl = Generate(new GenerateOptions());
        foreach (var command in new[] { "^LS", "^MM", "^MN", "^PO", "^JU" })
            Assert.DoesNotContain(command, zpl);
    }

    [Fact]
    public void Each_control_command_is_written_when_requested()
    {
        var zpl = Generate(new GenerateOptions
        {
            LabelShiftDots = -12, Mode = PrintMode.PeelOff, Tracking = MediaTracking.Mark,
            Inverted = true, ConfigUpdate = ConfigUpdate.Save,
        });
        Assert.Contains("^LS-12\n", zpl);
        Assert.Contains("^MMP\n", zpl);
        Assert.Contains("^MNM\n", zpl);
        Assert.Contains("^POI\n", zpl);
        // Saving to the printer's memory must come last so it stores the settings written before it.
        Assert.True(zpl.IndexOf("^JUS", StringComparison.Ordinal) > zpl.IndexOf("^POI", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(PrintMode.TearOff, "^MMT")]
    [InlineData(PrintMode.PeelOff, "^MMP")]
    [InlineData(PrintMode.Rewind, "^MMR")]
    [InlineData(PrintMode.Applicator, "^MMA")]
    [InlineData(PrintMode.Cutter, "^MMC")]
    [InlineData(PrintMode.DelayedCut, "^MMD")]
    public void Print_modes_map_to_their_letter(PrintMode mode, string expected) =>
        Assert.Contains(expected + "\n", Generate(new GenerateOptions { Mode = mode }));

    [Theory]
    [InlineData(MediaTracking.Continuous, "^MNN")]
    [InlineData(MediaTracking.Web, "^MNY")]
    [InlineData(MediaTracking.Mark, "^MNM")]
    [InlineData(MediaTracking.Auto, "^MNA")]
    public void Media_tracking_maps_to_its_letter(MediaTracking tracking, string expected) =>
        Assert.Contains(expected + "\n", Generate(new GenerateOptions { Tracking = tracking }));

    [Fact]
    public void Recall_is_written_as_JUR_and_there_is_no_factory_reset()
    {
        Assert.Contains("^JUR\n", Generate(new GenerateOptions { ConfigUpdate = ConfigUpdate.Recall }));
        // Factory reset (^JUF) would wipe a printer's setup, so the choices are only Save and Recall.
        Assert.Equal(new[] { "Save", "Recall" }, Enum.GetNames<ConfigUpdate>());
    }

    [Theory]
    [InlineData(10000)]
    [InlineData(-10000)]
    public void A_label_shift_out_of_range_is_refused_in_plain_words(int shift)
    {
        var ex = Assert.Throws<TemplateException>(() => Generate(new GenerateOptions { LabelShiftDots = shift }));
        Assert.Contains("label shift", ex.Message);
    }

    [Fact]
    public void The_preview_ignores_the_control_commands_without_a_warning()
    {
        var r = ZplRenderer.Render(Generate(new GenerateOptions
        {
            LabelShiftDots = 5, Mode = PrintMode.Cutter, Tracking = MediaTracking.Web, Inverted = false,
            ConfigUpdate = ConfigUpdate.Recall,
        }));
        Assert.Empty(r.Warnings);
    }
}
