using Netcodes.Printing;
using Netcodes.Template;

namespace Netcodes.Tests;

/// <summary>The quick-start in README.md, run for real (the dry run sends nothing to any printer).</summary>
public class ReadmeExampleTests
{
    [Fact]
    public async Task The_readme_quick_start_works_as_written()
    {
        var examples = DocsExamples.Directory();
        // Same code as README.md, with the example files addressed by path.
        var template = LabelTemplate.FromJson(File.ReadAllText(Path.Combine(examples, "shipping-label.template.json")));
        var data = File.ReadAllText(Path.Combine(examples, "shipping-label.data.json"));

        var engine = new NetcodesEngine();

        var zpl = engine.GenerateZpl(template, data);
        Assert.Empty(zpl.Warnings);

        var preview = engine.Preview(zpl.Zpl);
        Assert.NotEmpty(preview.Png);

        var printer = new PrinterTarget("Dock 1", Host: "192.168.1.50");
        var result = await engine.PrintAsync(printer, zpl.Zpl, copies: 1);
        Assert.True(result.Success);
        Assert.True(result.DryRun);
        Assert.Contains("Nothing was printed", result.Message);
    }
}

/// <summary>Finds the docs/guide/examples folder from wherever the tests run.</summary>
internal static class DocsExamples
{
    public static string Directory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Netcodes.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "docs", "guide", "examples");
    }
}
