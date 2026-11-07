using System.Runtime.Versioning;
using PDFtoImage;
using SkiaSharp;

namespace ClaimLens.Api.DocumentPreparation;

/// <summary>One page image for the viewer and AI, with the small copy the document rail shows.</summary>
public sealed record RenderedPage(byte[] Png, byte[] ThumbnailPng, int Width, int Height);

/// <summary>The bytes are not an image or PDF that can be drawn. The file is rejected with this reason.</summary>
public sealed class UnreadableFileException(string reason, Exception? inner = null) : Exception(reason, inner);

/// <summary>
/// Draws every page as a PNG. PDFs go through PDFium (PDFtoImage); JPG and PNG through SkiaSharp, turned upright by
/// their EXIF orientation first, because every bounding box is relative to the stored image (docs/10, section 4).
/// </summary>
public sealed class PageRenderer
{
    /// <summary>About A4 at 150 DPI: enough to read small print, small enough for the viewer.</summary>
    private const int PdfDpi = 150;

    /// <summary>A phone photo can be 8000 px wide; the page image keeps the long side at or under this.</summary>
    public const int MaxSide = 3000;

    /// <summary>
    /// The most pixels a photo is decoded at, about 200 MB of memory. A 20 MB file can declare far more than this.
    /// </summary>
    public const long MaxDecodedPixels = 50_000_000;

    /// <summary>The document rail's width for a page thumbnail.</summary>
    public const int ThumbnailWidth = 240;

    public int PdfPageCount(byte[] pdf)
    {
        if (!PdfiumAvailable)
        {
            throw new PlatformNotSupportedException(NoPdfium);
        }

        try
        {
            return Conversion.GetPageCount(pdf);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            throw new UnreadableFileException("The PDF could not be read.", error);
        }
    }

    public IReadOnlyList<RenderedPage> RenderPdf(byte[] pdf)
    {
        if (!PdfiumAvailable)
        {
            throw new PlatformNotSupportedException(NoPdfium);
        }

        try
        {
            var sizes = Conversion.GetPageSizes(pdf);
            var pages = new List<RenderedPage>(sizes.Count);
            for (var index = 0; index < sizes.Count; index++)
            {
                // Sizes are in points (1/72 inch). A poster-sized page is drawn at a lower DPI to stay within MaxSide.
                var longSide = Math.Max(sizes[index].Width, sizes[index].Height);
                var dpi = longSide <= 0 ? PdfDpi : Math.Max(1, Math.Min(PdfDpi, (int)(MaxSide * 72 / longSide)));
                using var bitmap = Conversion.ToImage(pdf, index, options: new RenderOptions(Dpi: dpi, WithAnnotations: true));
                pages.Add(Encode(bitmap));
            }

            return pages;
        }
        catch (Exception error) when (error is not (OutOfMemoryException or UnreadableFileException))
        {
            throw new UnreadableFileException("The PDF could not be read.", error);
        }
    }

    /// <summary>PDFium ships native libraries for these; the API runs on Windows for development and Linux in Docker.</summary>
    [SupportedOSPlatformGuard("windows")]
    [SupportedOSPlatformGuard("linux")]
    [SupportedOSPlatformGuard("macos")]
    private static bool PdfiumAvailable => OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();

    private const string NoPdfium = "PDF pages are drawn with PDFium, which this platform does not have.";

    public RenderedPage RenderImage(byte[] image)
    {
        using var data = SKData.CreateCopy(image);
        using var codec = SKCodec.Create(data) ?? throw new UnreadableFileException("The image could not be read.");
        var size = DecodeSize(codec);
        using var decoded = SKBitmap.Decode(codec, codec.Info.WithSize(size))
            ?? throw new UnreadableFileException("The image could not be read.");
        using var upright = Upright(decoded, codec.EncodedOrigin);
        using var page = Fit(upright, MaxSide);
        return Encode(page ?? upright);
    }

    /// <summary>
    /// The size to decode at, read from the header before any pixel is decoded. Above <see cref="MaxDecodedPixels"/> a
    /// JPEG is decoded at a smaller scale, which its codec does as it reads; a format that cannot do that is refused.
    /// </summary>
    public static SKSizeI DecodeSize(SKCodec codec)
    {
        var declared = new SKSizeI(codec.Info.Width, codec.Info.Height);
        if (Pixels(declared) <= MaxDecodedPixels)
        {
            return declared;
        }

        for (var scale = (float)Math.Sqrt(MaxDecodedPixels / (double)Pixels(declared)); scale > 0.01f; scale /= 2)
        {
            var scaled = codec.GetScaledDimensions(scale);
            if (scaled.Width > 0 && scaled.Height > 0 && Pixels(scaled) <= MaxDecodedPixels)
            {
                return scaled;
            }
        }

        throw new UnreadableFileException(
            $"The image is too large to read: it has {Pixels(declared) / 1_000_000} megapixels and at most {MaxDecodedPixels / 1_000_000} megapixels are accepted.");
    }

    private static long Pixels(SKSizeI size) => (long)size.Width * size.Height;

    private static RenderedPage Encode(SKBitmap page)
    {
        // A page narrower than the rail keeps its size rather than being blown up.
        var width = Math.Min(ThumbnailWidth, page.Width);
        var height = Math.Max(1, (int)Math.Round(page.Height * (double)width / page.Width));
        using var thumbnail = page.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));

        return new RenderedPage(Png(page), Png(thumbnail), page.Width, page.Height);
    }

    private static byte[] Png(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    /// <summary>A smaller copy when the long side is above the limit, otherwise null.</summary>
    private static SKBitmap? Fit(SKBitmap bitmap, int maxSide)
    {
        var longSide = Math.Max(bitmap.Width, bitmap.Height);
        if (longSide <= maxSide)
        {
            return null;
        }

        var scale = (double)maxSide / longSide;
        var info = new SKImageInfo(Math.Max(1, (int)(bitmap.Width * scale)), Math.Max(1, (int)(bitmap.Height * scale)));
        return bitmap.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
    }

    /// <summary>
    /// Applies the EXIF orientation, so the stored pixels are the way a person reads the page. Each matrix maps a source
    /// pixel (x, y) of a w × h image to where it belongs: x' = scaleX·x + skewX·y + transX, y' = skewY·x + scaleY·y + transY.
    /// </summary>
    public static SKBitmap Upright(SKBitmap source, SKEncodedOrigin origin)
    {
        float w = source.Width;
        float h = source.Height;
        var (matrix, swap) = origin switch
        {
            SKEncodedOrigin.TopRight => (new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1), false),
            SKEncodedOrigin.BottomRight => (new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1), false),
            SKEncodedOrigin.BottomLeft => (new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1), false),
            SKEncodedOrigin.LeftTop => (new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1), true),
            SKEncodedOrigin.RightTop => (new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1), true),
            SKEncodedOrigin.RightBottom => (new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1), true),
            SKEncodedOrigin.LeftBottom => (new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1), true),
            _ => (SKMatrix.Identity, false),
        };

        var result = new SKBitmap(
            swap ? source.Height : source.Width,
            swap ? source.Width : source.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.White);
        canvas.SetMatrix(matrix);
        using var image = SKImage.FromBitmap(source);
        canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
        return result;
    }
}
