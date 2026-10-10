using System.Net.Sockets;
using System.Text;

namespace Netcodes.Printing;

/// <summary>Sends ZPL to a printer. Safe by default: nothing is printed unless dry run is switched off.</summary>
public sealed class ZplPrinter
{
    private readonly TimeSpan _connectTimeout;

    /// <summary>Creates a printer client.</summary>
    /// <param name="connectTimeout">How long to wait for a printer to answer (default 5 seconds).</param>
    public ZplPrinter(TimeSpan? connectTimeout = null) => _connectTimeout = connectTimeout ?? TimeSpan.FromSeconds(5);

    /// <summary>Sends a label to a printer.</summary>
    /// <param name="target">The printer.</param>
    /// <param name="zpl">The label.</param>
    /// <param name="copies">Number of copies, 1 to 10000.</param>
    /// <param name="dryRun">When true (the default) the request is checked but nothing is sent. Set false to really print.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<PrintResult> PrintAsync(PrinterTarget target, string zpl, int copies = 1, bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(zpl))
            return Fail("There is nothing to print: the label is empty. Generate the label first.");
        if (copies is < 1 or > 10000)
            return Fail("The number of copies must be between 1 and 10000.");

        // Checked before the dry-run branch so a dry run reports the same refusal a real print would.
        var targetProblem = CheckTarget(target);
        if (targetProblem is not null)
            return Fail(targetProblem);

        var payload = ZplQuantity.Apply(zpl, copies);
        if (dryRun)
        {
            return new PrintResult(true, true,
                $"Dry run: the label was checked and would be sent to {target.Name} ({copies} cop{(copies == 1 ? "y" : "ies")}). " +
                "Nothing was printed. To really print, set dryRun to false.");
        }

        var bytes = Encoding.UTF8.GetBytes(payload);
        if (!string.IsNullOrWhiteSpace(target.Host))
            return await SendTcpAsync(target, bytes, copies, cancellationToken);
        if (!string.IsNullOrWhiteSpace(target.WindowsQueue))
            return WindowsRawPrinter.Send(target, bytes, copies);
        return Fail($"The printer \"{target.Name}\" has no address. Give it a host (for example 192.168.1.50) or a Windows printer name.");
    }

    private async Task<PrintResult> SendTcpAsync(PrinterTarget target, byte[] bytes, int copies, CancellationToken ct)
    {
        using var client = new TcpClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_connectTimeout);
        try
        {
            // The host was validated trimmed, so connect with the trimmed value too.
            await client.ConnectAsync(target.Host!.Trim(), target.Port, timeout.Token);
            var stream = client.GetStream();
            // A printer that accepts the connection and then stops reading must not hold the caller forever.
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            await stream.WriteAsync(bytes, timeout.Token);
            await stream.FlushAsync(timeout.Token);
            // Port 9100 has no reply, so "sent" is the most that can honestly be said.
            return new PrintResult(true, false,
                $"The label ({copies} cop{(copies == 1 ? "y" : "ies")}) was sent to {target.Name} at {target.Host}:{target.Port}. " +
                "The printer does not confirm, so check the printer if nothing comes out.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Fail(Unreachable(target));
        }
        catch (SocketException)
        {
            return Fail(Unreachable(target));
        }
        catch (IOException)
        {
            // Busy, rebooting or in an error state: the printer accepted the connection and then dropped it.
            return Fail($"The connection to {target.Name} was lost while the label was being sent. Check the printer for an error or paper out, then try again.");
        }
    }

    // Printer names and addresses can come from an API caller, so they are untrusted. This only checks the shape;
    // deciding WHICH hosts a caller may reach (for example only saved printers) belongs to the service on top.
    private static readonly System.Text.RegularExpressions.Regex HostName =
        new(@"^[A-Za-z0-9]([A-Za-z0-9.\-]*[A-Za-z0-9])?$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string? CheckTarget(PrinterTarget t)
    {
        if (!string.IsNullOrWhiteSpace(t.Host))
        {
            var host = t.Host.Trim();
            if (!System.Net.IPAddress.TryParse(host, out _) && !HostName.IsMatch(host))
                return $"\"{t.Host}\" is not a valid printer address. Use an IP address such as 192.168.1.50 or a host name such as zebra-dock1.";
            if (t.Port is < 1 or > 65535)
                return $"The port {t.Port} is not a valid port number. Zebra printers use 9100.";
        }
        if (string.IsNullOrWhiteSpace(t.Host) && string.IsNullOrWhiteSpace(t.WindowsQueue))
            return $"The printer \"{t.Name}\" has no address. Give it a host (for example 192.168.1.50) or a Windows printer name.";
        // A UNC name (\\server\printer) makes Windows open a network connection and send the user's credentials
        // to that server. Spellings of it vary (//server/x, \\?\UNC\..., leading whitespace or control characters), so
        // this is an allowlist, not a check for one prefix: a local printer name never contains a slash, a backslash,
        // a comma or a control character, and anything that does is refused.
        if (t.WindowsQueue is { } queue && queue.Any(c => c is '\\' or '/' or ',' || char.IsControl(c)))
            return "That Windows printer name is not allowed. Use the printer's local name as shown in Settings > Printers (network shares like \\\\server\\printer are not allowed), or use the printer's IP address.";
        return null;
    }

    private static string Unreachable(PrinterTarget t) =>
        $"The printer {t.Host} did not answer on port {t.Port}. Check that it is on and on the same network, " +
        "and that the address is right.";

    private static PrintResult Fail(string message) => new(false, false, message);
}
