namespace Netcodes.Printing;

/// <summary>Where a label is sent: a network printer (host and port) or a Windows printer queue.</summary>
/// <param name="Name">A friendly name used in messages, for example "Dock 1".</param>
/// <param name="Host">IP address or host name of a network printer.</param>
/// <param name="Port">TCP port; Zebra printers listen for raw ZPL on 9100.</param>
/// <param name="WindowsQueue">The name of a printer installed in Windows (used when no host is given).</param>
public sealed record PrinterTarget(string Name, string? Host = null, int Port = 9100, string? WindowsQueue = null);

/// <summary>The outcome of a print request.</summary>
/// <param name="Success">True when the request succeeded (for a dry run: when it passed the checks).</param>
/// <param name="DryRun">True when nothing was sent to a printer.</param>
/// <param name="Message">What happened and what to do next, in plain language.</param>
public sealed record PrintResult(bool Success, bool DryRun, string Message);
