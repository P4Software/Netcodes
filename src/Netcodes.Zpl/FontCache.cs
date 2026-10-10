using SkiaSharp;

namespace Netcodes.Zpl;

/// <summary>Loads the embedded preview fonts once.</summary>
internal static class FontCache
{
    private static readonly Lazy<SKTypeface> Sans = new(() => Load("DejaVuSans.ttf"));
    private static readonly Lazy<SKTypeface> Mono = new(() => Load("DejaVuSansMono.ttf"));

    /// <summary>The monospaced face, used for Zebra's fixed-size bitmap fonts A to H and for barcode captions.</summary>
    public static SKTypeface Fixed => Mono.Value;

    /// <summary>Chooses the face that best imitates a ZPL font: bitmap fonts A-H are fixed-width, font 0 is scalable.</summary>
    public static SKTypeface For(string zplFont) =>
        zplFont.Length == 1 && zplFont[0] is >= 'A' and <= 'H' ? Mono.Value : Sans.Value;

    private static SKTypeface Load(string file)
    {
        using var stream = typeof(FontCache).Assembly.GetManifestResourceStream("Netcodes.Zpl.Fonts." + file);
        if (stream is null) return SKTypeface.Default;
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        // Copy the bytes so the typeface does not depend on a stream that is about to be disposed.
        return SKTypeface.FromData(SKData.CreateCopy(copy.ToArray())) ?? SKTypeface.Default;
    }
}
