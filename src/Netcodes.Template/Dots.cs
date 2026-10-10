namespace Netcodes.Template;

/// <summary>Converts physical sizes to printer dots.</summary>
public static class Dots
{
    /// <summary>Converts millimetres to dots at the given resolution, rounding to the nearest dot.</summary>
    /// <param name="mm">Length in millimetres.</param>
    /// <param name="dpi">Printer resolution in dots per inch.</param>
    public static int FromMm(double mm, int dpi) =>
        (int)Math.Round(mm * dpi / 25.4, MidpointRounding.AwayFromZero);
}
