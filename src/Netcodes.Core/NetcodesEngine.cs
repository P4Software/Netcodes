using Netcodes.Printing;
using Netcodes.Template;
using Netcodes.Zpl;

namespace Netcodes;

/// <summary>
/// The programmer-facing entry point: generate ZPL from a template, preview it, and print it.
/// Everything runs in-process, with no server.
/// </summary>
public sealed class NetcodesEngine
{
    private readonly ZplPrinter _printer;

    /// <summary>Creates an engine.</summary>
    /// <param name="printer">A printer client; the default waits 5 seconds for a printer to answer.</param>
    public NetcodesEngine(ZplPrinter? printer = null) => _printer = printer ?? new ZplPrinter();

    /// <summary>Generates ZPL from a template and JSON data.</summary>
    /// <param name="template">The label design.</param>
    /// <param name="dataJson">JSON object that fills the placeholders.</param>
    /// <param name="options">Optional printer settings.</param>
    /// <exception cref="TemplateException">The template or the data is unusable; the message says why.</exception>
    public ZplResult GenerateZpl(LabelTemplate template, string dataJson, GenerateOptions? options = null) =>
        ZplGenerator.Generate(template, dataJson, options);

    /// <summary>Draws hand-written or generated ZPL as a PNG preview.</summary>
    /// <param name="zpl">The ZPL to draw.</param>
    /// <param name="options">Optional preview settings.</param>
    public RenderResult Preview(string zpl, RenderOptions? options = null) => ZplRenderer.Render(zpl, options);

    /// <summary>Generates ZPL from a template and draws it; generator and preview warnings are combined.</summary>
    /// <param name="template">The label design.</param>
    /// <param name="dataJson">JSON object that fills the placeholders.</param>
    public RenderResult Preview(LabelTemplate template, string dataJson)
    {
        var generated = ZplGenerator.Generate(template, dataJson);
        var rendered = ZplRenderer.Render(generated.Zpl);
        return rendered with { Warnings = generated.Warnings.Concat(rendered.Warnings).ToList() };
    }

    /// <summary>Sends ZPL to a printer. Nothing is printed unless <paramref name="dryRun"/> is set to false.</summary>
    /// <param name="target">The printer.</param>
    /// <param name="zpl">The label.</param>
    /// <param name="copies">Number of copies.</param>
    /// <param name="dryRun">True (default) only checks the request; false really prints.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<PrintResult> PrintAsync(PrinterTarget target, string zpl, int copies = 1, bool dryRun = true,
        CancellationToken cancellationToken = default) =>
        _printer.PrintAsync(target, zpl, copies, dryRun, cancellationToken);
}
