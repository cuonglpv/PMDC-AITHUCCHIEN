using System.Buffers.Binary;
using System.IO.Compression;
using ClaimLens.Api.DocumentPreparation;
using SkiaSharp;

namespace ClaimLens.Api.Tests.Documents;

/// <summary>
/// A 20 MB upload can declare dimensions whose decoded pixels would not fit in memory. The renderer reads the size from
/// the header first, then decodes scaled down where the format allows it, and refuses the file where it does not.
/// </summary>
public class PageRendererTests
{
    private readonly PageRenderer renderer = new();

    [Fact]
    public void A_PNG_that_declares_more_pixels_than_the_cap_is_refused_before_it_is_decoded()
    {
        var png = PngDeclaring(20_000, 20_000);

        var error = Assert.Throws<UnreadableFileException>(() => renderer.RenderImage(png));

        Assert.Contains("too large", error.Message, StringComparison.Ordinal);
        Assert.Contains("50 megapixels", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_JPEG_that_declares_more_pixels_than_the_cap_is_decoded_scaled_down()
    {
        var jpg = JpegDeclaring(TestFiles.Jpg(64, 64), 16_000, 12_000);
        using var codec = SKCodec.Create(SKData.CreateCopy(jpg));

        var decodeSize = PageRenderer.DecodeSize(codec);
        var page = renderer.RenderImage(jpg);

        Assert.Equal((16_000, 12_000), (codec.Info.Width, codec.Info.Height));
        Assert.True((long)decodeSize.Width * decodeSize.Height <= PageRenderer.MaxDecodedPixels, $"Decoded at {decodeSize}.");

        Assert.True((long)page.Width * page.Height <= PageRenderer.MaxDecodedPixels);
        Assert.True(Math.Max(page.Width, page.Height) <= PageRenderer.MaxSide);
        Assert.Equal(4, Math.Round(page.Width / (double)page.Height * 3));
    }

    [Fact]
    public void An_ordinary_photo_keeps_its_size()
    {
        var page = renderer.RenderImage(TestFiles.Jpg(1200, 800));

        Assert.Equal((1200, 800), (page.Width, page.Height));
    }

    /// <summary>A valid PNG header and one tiny image chunk: the codec opens it and reports the declared size.</summary>
    private static byte[] PngDeclaring(int width, int height)
    {
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; // bit depth
        header[9] = 2; // RGB

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(new byte[1 + (width * 3)]);
        }

        return
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            .. Chunk("IHDR", header),
            .. Chunk("IDAT", compressed.ToArray()),
            .. Chunk("IEND", []),
        ];
    }

    private static byte[] Chunk(string type, byte[] data)
    {
        var chunk = new byte[12 + data.Length];
        BinaryPrimitives.WriteInt32BigEndian(chunk, data.Length);
        System.Text.Encoding.ASCII.GetBytes(type).CopyTo(chunk, 4);
        data.CopyTo(chunk, 8);
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(8 + data.Length), Crc32(chunk.AsSpan(4, 4 + data.Length)));
        return chunk;
    }

    private static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }

    /// <summary>A real small JPEG whose frame header is changed to claim another size.</summary>
    private static byte[] JpegDeclaring(byte[] jpg, ushort width, ushort height)
    {
        var copy = jpg.ToArray();
        for (var index = 2; index < copy.Length - 9; index++)
        {
            if (copy[index] == 0xFF && copy[index + 1] is 0xC0 or 0xC2)
            {
                BinaryPrimitives.WriteUInt16BigEndian(copy.AsSpan(index + 5), height);
                BinaryPrimitives.WriteUInt16BigEndian(copy.AsSpan(index + 7), width);
                return copy;
            }
        }

        throw new InvalidOperationException("No frame header found.");
    }
}
