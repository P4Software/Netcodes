using System.Drawing.Printing;
using System.Runtime.Versioning;

namespace Netcodes.Printing;

/// <summary>Finds printers installed on this machine.</summary>
public static class PrinterDiscovery
{
    /// <summary>Names of printers installed in Windows; empty on other systems or if the list cannot be read.</summary>
    public static IReadOnlyList<string> WindowsQueues()
    {
        if (!OperatingSystem.IsWindows()) return Array.Empty<string>();
        try
        {
            return ReadInstalledPrinters();
        }
        catch (Exception)
        {
            // A locked-down spooler must not break the caller; an empty list simply means "none found".
            return Array.Empty<string>();
        }
    }

    // Separate method so the platform check above is visible to the compiler's platform analyzer (no CA1416 warning).
    [SupportedOSPlatform("windows")]
    private static List<string> ReadInstalledPrinters() => PrinterSettings.InstalledPrinters.Cast<string>().ToList();
}
