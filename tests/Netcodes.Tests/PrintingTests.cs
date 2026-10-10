using System.Net;
using System.Net.Sockets;
using System.Text;
using Netcodes.Printing;

namespace Netcodes.Tests;

public class PrintingTests
{
    [Theory]
    [InlineData("^XA^FDhi^FS^XZ", 1, "^XA^FDhi^FS^XZ")]
    [InlineData("^XA^FDhi^FS^XZ", 3, "^XA^FDhi^FS^PQ3^XZ")]
    [InlineData("^XA^PQ5^XZ", 2, "^XA^PQ2^XZ")]
    public void Quantity_is_inserted_before_the_end_of_the_label(string zpl, int copies, string expected)
    {
        Assert.Equal(expected, ZplQuantity.Apply(zpl, copies));
    }

    [Fact]
    public async Task Dry_run_sends_nothing_and_says_so()
    {
        // The host does not exist; a dry run must not even try to connect.
        var r = await new ZplPrinter().PrintAsync(new PrinterTarget("Dock 1", "203.0.113.9"), "^XA^XZ");
        Assert.True(r.Success);
        Assert.True(r.DryRun);
        Assert.Contains("Nothing was printed", r.Message);
        Assert.Contains("dryRun to false", r.Message);
    }

    [Fact]
    public async Task Sends_the_zpl_over_tcp_with_the_quantity_applied()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var received = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var ms = new MemoryStream();
            await client.GetStream().CopyToAsync(ms);
            return Encoding.UTF8.GetString(ms.ToArray());
        });

        var r = await new ZplPrinter().PrintAsync(
            new PrinterTarget("Dock 1", "127.0.0.1", port), "^XA^FDhi^FS^XZ", copies: 3, dryRun: false);

        Assert.Equal("^XA^FDhi^FS^PQ3^XZ", await received);
        listener.Stop();
        Assert.True(r.Success);
        Assert.False(r.DryRun);
        Assert.Contains("Dock 1", r.Message);
    }

    [Fact]
    public async Task A_printer_that_is_off_gives_a_plain_message_with_its_address()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();   // nothing listens on this port any more

        var r = await new ZplPrinter(TimeSpan.FromSeconds(2)).PrintAsync(
            new PrinterTarget("Dock 1", "127.0.0.1", port), "^XA^XZ", dryRun: false);

        Assert.False(r.Success);
        Assert.Contains("127.0.0.1", r.Message);
        Assert.Contains("Check that it is on", r.Message);
    }

    [Theory]
    [InlineData("", 1, "nothing to print")]
    [InlineData("^XA^XZ", 0, "between 1 and 10000")]
    [InlineData("^XA^XZ", 10001, "between 1 and 10000")]
    public async Task Bad_requests_are_refused_in_plain_language(string zpl, int copies, string mention)
    {
        var r = await new ZplPrinter().PrintAsync(new PrinterTarget("P", "127.0.0.1"), zpl, copies, dryRun: false);
        Assert.False(r.Success);
        Assert.Contains(mention, r.Message);
    }

    [Fact]
    public async Task A_printer_without_an_address_explains_what_to_add()
    {
        var r = await new ZplPrinter().PrintAsync(new PrinterTarget("P"), "^XA^XZ", dryRun: false);
        Assert.False(r.Success);
        Assert.Contains("no address", r.Message);
    }

    [Fact]
    public void Listing_windows_printers_never_throws()
    {
        Assert.NotNull(PrinterDiscovery.WindowsQueues());
    }
}
