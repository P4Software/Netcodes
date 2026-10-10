using Netcodes.Printing;
using Netcodes.Zpl;

namespace Netcodes.Tests;

/// <summary>
/// ZPL, printer addresses and printer names can come from an API caller, so they are untrusted input.
/// </summary>
public class SecurityLimitsTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_unc_printer_share_is_refused_so_windows_never_connects_out(bool dryRun)
    {
        // A name like \attacker\share makes Windows open an SMB connection and send the user's credentials.
        var target = new PrinterTarget("P", WindowsQueue: @"\\attacker.example\share");
        var r = await new ZplPrinter().PrintAsync(target, "^XA^XZ", dryRun: dryRun);
        Assert.False(r.Success, r.Message);
        Assert.Contains("not allowed", r.Message);
    }

    // Review finding: a denylist on a leading "\\" misses other spellings Windows may resolve to a remote server.
    // Local printer names never contain slashes, commas or control characters, so anything else is refused.
    [Theory]
    [InlineData("//attacker.example/share")]
    [InlineData("\\\\?\\UNC\\attacker.example\\share")]
    [InlineData("\t\\\\attacker.example\\share")]
    [InlineData("Zebra\\..\\other")]
    [InlineData("Zebra,LocalsplOnly")]
    [InlineData("Zebra\u0000\\\\attacker.example\\share")]
    public async Task Printer_names_with_path_or_control_characters_are_refused(string queue)
    {
        var r = await new ZplPrinter().PrintAsync(new PrinterTarget("P", WindowsQueue: queue), "^XA^XZ", dryRun: false);
        Assert.False(r.Success, r.Message);
        Assert.Contains("not allowed", r.Message);
    }

    [Fact]
    public async Task An_ordinary_local_printer_name_is_accepted_by_the_check()
    {
        var r = await new ZplPrinter().PrintAsync(new PrinterTarget("P", WindowsQueue: "ZDesigner ZD421-203dpi ZPL"), "^XA^XZ");
        Assert.True(r.Success);   // dry run
    }

    [Theory]
    [InlineData("http://printer.local", 9100)]
    [InlineData("host name with spaces", 9100)]
    [InlineData("192.168.1.5/path", 9100)]
    [InlineData("192.168.1.5", 0)]
    [InlineData("192.168.1.5", 70000)]
    public async Task Malformed_addresses_and_ports_are_refused_before_any_connection(string host, int port)
    {
        var r = await new ZplPrinter().PrintAsync(new PrinterTarget("P", host, port), "^XA^XZ", dryRun: false);
        Assert.False(r.Success);
        Assert.Contains("not a valid", r.Message);
    }

    [Theory]
    [InlineData("192.168.1.50")]
    [InlineData("zebra-dock1.warehouse.local")]
    [InlineData("::1")]
    public async Task Ordinary_hosts_are_accepted_by_the_check(string host)
    {
        var r = await new ZplPrinter().PrintAsync(new PrinterTarget("P", host), "^XA^XZ");   // dry run
        Assert.True(r.Success);
    }

    [Fact]
    public void The_parser_stops_at_five_thousand_fields()
    {
        var zpl = "^XA" + string.Concat(Enumerable.Repeat("^FO0,0^GB1,1,1^FS", 20000)) + "^XZ";
        var doc = ZplParser.Parse(zpl);
        Assert.Equal(5000, doc.Ops.Count);
        Assert.Contains(doc.Warnings, w => w.Contains("too many fields"));
    }

    [Fact]
    public void Oversized_input_is_cut_off_with_a_warning()
    {
        var zpl = new string(' ', 3_000_000) + "^XA^FO0,0^FDx^FS^XZ";
        var doc = ZplParser.Parse(zpl);
        Assert.Empty(doc.Ops);
        Assert.Contains(doc.Warnings, w => w.Contains("too large"));
    }
}
