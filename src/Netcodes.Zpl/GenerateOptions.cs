namespace Netcodes.Zpl;

/// <summary>Whether the printer burns the label with a ribbon or heats special paper.</summary>
public enum PrinterMedia
{
    /// <summary>Thermal transfer (ribbon).</summary>
    Thermal,
    /// <summary>Direct thermal (heat-sensitive media, no ribbon).</summary>
    Direct,
}

/// <summary>What the printer does with a printed label (<c>^MM</c>).</summary>
public enum PrintMode
{
    /// <summary>Tear off the label by hand (T).</summary>
    TearOff,
    /// <summary>Peel the label off its backing (P).</summary>
    PeelOff,
    /// <summary>Rewind the printed labels on the printer's spindle (R).</summary>
    Rewind,
    /// <summary>Hand the label to an applicator (A).</summary>
    Applicator,
    /// <summary>Cut after each label (C).</summary>
    Cutter,
    /// <summary>Cut when the next label is requested (D).</summary>
    DelayedCut,
}

/// <summary>How the printer finds the start of each label (<c>^MN</c>).</summary>
public enum MediaTracking
{
    /// <summary>Continuous media with no gaps or marks (N).</summary>
    Continuous,
    /// <summary>Labels separated by a gap, found by the web sensor (Y).</summary>
    Web,
    /// <summary>Labels marked with a black mark on the back (M).</summary>
    Mark,
    /// <summary>The printer decides while calibrating (A).</summary>
    Auto,
}

/// <summary>
/// What to do with the printer's stored configuration (<c>^JU</c>). Save writes the printer's permanent memory,
/// which wears out after many writes, so use it for setup labels, not on every label. There is deliberately no
/// factory reset here.
/// </summary>
public enum ConfigUpdate
{
    /// <summary>Save the current settings (S).</summary>
    Save,
    /// <summary>Load the last saved settings (R).</summary>
    Recall,
}

/// <summary>
/// Optional printer settings written into the generated ZPL. Nothing here is written unless it is set, so a
/// plain label never overrides the printer's own configuration.
/// </summary>
public sealed class GenerateOptions
{
    /// <summary>Print speed, 1 to 14 (<c>^PR</c>).</summary>
    public int? PrintSpeed { get; init; }
    /// <summary>Print darkness, 0 to 30 (<c>^MD</c> adjusts relative to the printer's setting).</summary>
    public int? Darkness { get; init; }
    /// <summary>Media type (<c>^MT</c>).</summary>
    public PrinterMedia? Media { get; init; }
    /// <summary>Number of copies, 1 to 10000 (<c>^PQ</c>).</summary>
    public int? Quantity { get; init; }
    /// <summary>Moves everything left (negative) or right (positive) by this many dots, -9999 to 9999 (<c>^LS</c>).</summary>
    public int? LabelShiftDots { get; init; }
    /// <summary>What the printer does with each printed label (<c>^MM</c>).</summary>
    public PrintMode? Mode { get; init; }
    /// <summary>How the printer finds the start of each label (<c>^MN</c>).</summary>
    public MediaTracking? Tracking { get; init; }
    /// <summary>True prints the label turned 180 degrees (<c>^PO</c>).</summary>
    public bool? Inverted { get; init; }
    /// <summary>Saves or recalls the printer's stored settings (<c>^JU</c>); see <see cref="Netcodes.Zpl.ConfigUpdate"/>.</summary>
    public ConfigUpdate? ConfigUpdate { get; init; }
}
