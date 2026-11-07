using System.IO.Compression;
using ClaimLens.Api.Domain.Enums;

namespace ClaimLens.Api.DocumentPreparation;

/// <summary>
/// Tells a file's type by its first bytes, never by its name or the browser's content type. LibreOffice turns any bytes
/// into a PDF, so a renamed or corrupted file would otherwise become a page of garbage (docs/10-code-structure.md, 13).
/// </summary>
public static class FileSignature
{
    private static ReadOnlySpan<byte> Jpg => [0xFF, 0xD8, 0xFF];
    private static ReadOnlySpan<byte> Png => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static ReadOnlySpan<byte> Pdf => "%PDF"u8;
    private static ReadOnlySpan<byte> Zip => [0x50, 0x4B, 0x03, 0x04];

    /// <summary>The format, or null when the bytes are none of JPG, PNG, PDF, DOCX and XLSX.</summary>
    public static FileFormat? Detect(byte[] content)
    {
        var start = content.AsSpan();
        if (start.StartsWith(Jpg))
        {
            return FileFormat.Jpg;
        }

        if (start.StartsWith(Png))
        {
            return FileFormat.Png;
        }

        if (start.StartsWith(Pdf))
        {
            return FileFormat.Pdf;
        }

        return start.StartsWith(Zip) ? OfficeFormat(content) : null;
    }

    /// <summary>An Office Open XML package has [Content_Types].xml, and word/ or xl/ for its main part.</summary>
    private static FileFormat? OfficeFormat(byte[] content)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(content, writable: false), ZipArchiveMode.Read);
            var names = zip.Entries.Select(entry => entry.FullName).ToList();
            if (!names.Contains("[Content_Types].xml", StringComparer.Ordinal))
            {
                return null;
            }

            var word = names.Any(name => name.StartsWith("word/", StringComparison.Ordinal));
            var excel = names.Any(name => name.StartsWith("xl/", StringComparison.Ordinal));
            return (word, excel) switch
            {
                (true, false) => FileFormat.Docx,
                (false, true) => FileFormat.Xlsx,
                _ => null,
            };
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }
}
