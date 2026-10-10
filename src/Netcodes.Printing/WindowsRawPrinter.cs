using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Netcodes.Printing;

/// <summary>Sends raw bytes to a printer installed in Windows, bypassing the driver (so ZPL reaches the printer untouched).</summary>
internal static class WindowsRawPrinter
{
    public static PrintResult Send(PrinterTarget target, byte[] bytes, int copies)
    {
        if (!OperatingSystem.IsWindows())
            return new PrintResult(false, false, "Windows printer queues only work on Windows. Use a network printer address instead.");
        return SendWindows(target, bytes, copies);
    }

    [SupportedOSPlatform("windows")]
    private static PrintResult SendWindows(PrinterTarget target, byte[] bytes, int copies)
    {
        var queue = target.WindowsQueue!;
        if (!OpenPrinter(queue, out var handle, IntPtr.Zero))
            return new PrintResult(false, false, $"Windows has no printer called \"{queue}\". Check the name in Settings > Printers.");
        try
        {
            var doc = new DocInfo { DocName = "Netcodes label", DataType = "RAW" };
            if (StartDocPrinter(handle, 1, ref doc) == 0 || !StartPagePrinter(handle))
                return new PrintResult(false, false, $"Windows could not start a print job on \"{queue}\". Check that the printer is online.");
            var ok = WritePrinter(handle, bytes, bytes.Length, out _);
            EndPagePrinter(handle);
            EndDocPrinter(handle);
            return ok
                ? new PrintResult(true, false, $"The label ({copies} cop{(copies == 1 ? "y" : "ies")}) was sent to the Windows printer \"{queue}\".")
                : new PrintResult(false, false, $"Windows accepted the job but could not write to \"{queue}\". Check that the printer is online.");
        }
        finally
        {
            ClosePrinter(handle);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DocInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string DocName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? OutputFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string DataType;
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string name, out IntPtr handle, IntPtr defaults);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int StartDocPrinter(IntPtr handle, int level, ref DocInfo info);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr handle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr handle, byte[] data, int count, out int written);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr handle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr handle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr handle);
}
