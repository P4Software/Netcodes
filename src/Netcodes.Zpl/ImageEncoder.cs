using SkiaSharp;

namespace Netcodes.Zpl;

/// <summary>A picture that cannot be turned into dots; the message says why in plain words.</summary>
internal sealed class ImageException(string message) : Exception(message);

/// <summary>
/// What one label may spend on pictures. Why: each picture is capped, but a template can hold thousands of them, and
/// decoding a picture costs time and memory in proportion to its source size, not to what it prints as. The budget
/// is shared by every image of one label (including those in repeat rows).
/// </summary>
internal sealed class ImageBudget
{
    /// <summary>Source pixels that may still be decoded (100 million: four of the largest allowed pictures).</summary>
    public long SourcePixels = 100_000_000;
    /// <summary>Characters of picture text that may still be decoded (20 million: a hundred 150 KB logos).</summary>
    public long SourceChars = 20_000_000;
    /// <summary>Bytes of printed graphic that may still be produced.</summary>
    public long OutputBytes = GraphicField.MaxTotalBytes;
}

/// <summary>Turns a base64 picture into a ready-to-write <c>^GF</c> field.</summary>
internal static class ImageEncoder
{
    // Limits, all because the picture is untrusted input.
    private const int MaxBase64Chars = 7_000_000;           // about 5 MB of picture
    private const long MaxSourcePixels = 25_000_000;        // reject decompression bombs from the header alone
    private const int MaxOutputSide = 4000;                  // the widest label the preview draws

    /// <summary>Converts a picture to a <c>^GFA</c> field (without the leading position or the closing <c>^FS</c>).</summary>
    /// <param name="source">Base64 text or a <c>data:</c> address.</param>
    /// <param name="widthDots">Printed width in dots.</param>
    /// <param name="heightDots">Printed height in dots, or null to keep the picture's shape.</param>
    /// <param name="threshold">Brightness below which a dot is black.</param>
    /// <param name="invert">Swap black and white.</param>
    /// <param name="rotation">0, 90, 180 or 270 degrees, applied before scaling.</param>
    /// <exception cref="ImageException">The picture cannot be used.</exception>
    /// <param name="budget">What the label may still spend on pictures; the picture's cost is taken from it.</param>
    public static string ToGraphicField(string source, int widthDots, int? heightDots, int threshold, bool invert, int rotation,
        ImageBudget budget)
    {
        // The text costs work to decode whatever the picture inside it looks like (a 1x1 picture followed by megabytes of
        // junk is a valid picture), so its length is charged before anything is decoded.
        if (source.Length > budget.SourceChars)
            throw new ImageException("The label has too many images (or their data is too large in total), so this image was left out. Use fewer or smaller pictures.");
        budget.SourceChars -= source.Length;
        var bytes = Decode(source);
        using var stream = new SKMemoryStream(bytes);
        using var codec = SKCodec.Create(stream)
            ?? throw new ImageException("The image data is not a picture Netcodes can read. Use PNG, JPEG, GIF, BMP or WebP.");
        if (codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Gif
                or SKEncodedImageFormat.Bmp or SKEncodedImageFormat.Webp))
            throw new ImageException("The image is in a format Netcodes does not accept. Use PNG, JPEG, GIF, BMP or WebP.");
        // Size check on the header, before any pixel is decoded.
        if ((long)codec.Info.Width * codec.Info.Height > MaxSourcePixels)
            throw new ImageException($"The image is too large ({codec.Info.Width} x {codec.Info.Height} pixels). Use a picture under {MaxSourcePixels / 1_000_000} million pixels.");

        var sourcePixels = (long)codec.Info.Width * codec.Info.Height;
        if (sourcePixels > budget.SourcePixels)
            throw new ImageException("The label has too many images (or they are too large in total), so this image was left out. Use fewer or smaller pictures.");
        budget.SourcePixels -= sourcePixels;

        using var decoded = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        if (codec.GetPixels(decoded.Info, decoded.GetPixels()) is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            throw new ImageException("The image data is not a picture Netcodes can read. Use PNG, JPEG, GIF, BMP or WebP.");
        // No rotation needs no second bitmap: a copy of a 5000 x 5000 picture would cost another 100 MB.
        using var rotated = rotation == 0 ? null : Rotate(decoded, rotation);
        var turned = rotated ?? decoded;

        var width = Math.Max(1, widthDots);
        var height = Math.Max(1, heightDots ??
            (int)Math.Round(width * (double)turned.Height / turned.Width, MidpointRounding.AwayFromZero));
        var bytesPerRow = (width + 7) / 8;
        if (width > MaxOutputSide || height > MaxOutputSide || (long)bytesPerRow * height > GraphicField.MaxBytes)
            throw new ImageException($"The image is too large to print here ({width} x {height} dots; the most is {MaxOutputSide} x {MaxOutputSide}). Make it smaller.");

        if ((long)bytesPerRow * height > budget.OutputBytes)
            throw new ImageException("The label has too many images (or they are too large in total), so this image was left out. Use fewer or smaller pictures.");
        budget.OutputBytes -= (long)bytesPerRow * height;

        // Draw onto white: transparent pixels must print as white, not as the black they hold underneath.
        using var flat = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(flat))
        {
            canvas.Clear(SKColors.White);
            using var image = SKImage.FromBitmap(turned);
            canvas.DrawImage(image, new SKRect(0, 0, width, height),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), null);
        }

        var bits = new byte[bytesPerRow * height];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var c = flat.GetPixel(x, y);
                var black = 0.299 * c.Red + 0.587 * c.Green + 0.114 * c.Blue < threshold;
                if (black != invert) bits[y * bytesPerRow + x / 8] |= (byte)(0x80 >> (x % 8));
            }
        return $"^GFA,{bits.Length},{bits.Length},{bytesPerRow},{GraphicField.Encode(bits, bytesPerRow)}";
    }

    private static byte[] Decode(string source)
    {
        var text = source.Trim();
        var comma = text.IndexOf(',');
        // "data:image/png;base64,AAAA": drop everything up to the comma.
        if (text.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0) text = text[(comma + 1)..];
        if (text.Length == 0) throw new ImageException("An image has no picture data. Give the picture as base64 text.");
        if (text.Length > MaxBase64Chars) throw new ImageException("The image data is too large. Use a picture under about 5 MB.");
        var buffer = new byte[text.Length * 3 / 4 + 3];
        if (!Convert.TryFromBase64String(text, buffer, out var written))
            throw new ImageException("The image data is not a picture: it is not valid base64 text. Give the picture as base64 or a data:image address (file paths and web addresses are not accepted).");
        return buffer[..written];
    }

    private static SKBitmap Rotate(SKBitmap source, int rotation)
    {
        var swap = rotation is 90 or 270;
        var result = new SKBitmap(new SKImageInfo(swap ? source.Height : source.Width, swap ? source.Width : source.Height,
            SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        switch (rotation)
        {
            case 90: canvas.Translate(source.Height, 0); break;
            case 180: canvas.Translate(source.Width, source.Height); break;
            default: canvas.Translate(0, source.Width); break;
        }
        canvas.RotateDegrees(rotation);
        using var image = SKImage.FromBitmap(source);
        canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest), null);
        return result;
    }
}
