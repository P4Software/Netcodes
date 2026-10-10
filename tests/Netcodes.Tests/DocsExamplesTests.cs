using Netcodes.Template;
using Netcodes.Zpl;

namespace Netcodes.Tests;

/// <summary>
/// Keeps the user guides honest: every example template in docs/guide/examples is run against the real engine, so a
/// guide that shows something the engine no longer accepts (or warns about) fails the build.
/// </summary>
public class DocsExamplesTests
{
    private static string ExamplesDirectory()
    {
        // The tests run from bin/Debug/..., so walk up to the folder that holds the solution.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Netcodes.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "docs", "guide", "examples");
    }

    public static IEnumerable<object[]> Examples() =>
        Directory.GetFiles(ExamplesDirectory(), "*.template.json")
            .Select(f => new object[] { Path.GetFileName(f).Replace(".template.json", "") });

    [Fact]
    public void The_guide_has_examples()
    {
        Assert.True(Examples().Count() >= 5, "docs/guide/examples should hold the example templates");
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public void Every_example_generates_and_previews_without_warnings(string name)
    {
        var dir = ExamplesDirectory();
        var template = LabelTemplate.FromJson(File.ReadAllText(Path.Combine(dir, name + ".template.json")));
        var dataPath = Path.Combine(dir, name + ".data.json");
        var data = File.Exists(dataPath) ? File.ReadAllText(dataPath) : "{}";

        Assert.Empty(template.Validate());
        var generated = ZplGenerator.Generate(template, data);
        Assert.Empty(generated.Warnings);
        var rendered = ZplRenderer.Render(generated.Zpl);
        Assert.Empty(rendered.Warnings);
        Assert.NotEmpty(rendered.Png);
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public void Every_example_survives_a_json_round_trip(string name)
    {
        var json = File.ReadAllText(Path.Combine(ExamplesDirectory(), name + ".template.json"));
        var first = LabelTemplate.FromJson(json);
        var again = LabelTemplate.FromJson(first.ToJson());
        Assert.Equal(first.ToJson(), again.ToJson());
    }
}
