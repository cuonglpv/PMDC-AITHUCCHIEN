using System.IO.Compression;
using System.Security;
using System.Text;
using SkiaSharp;

namespace ClaimLens.Api.Tests.Documents;

/// <summary>Small files made on the fly, so the tests need no fixtures on disk.</summary>
public static class TestFiles
{
    /// <summary>A JPG whose left half is red and right half blue, optionally with an EXIF orientation.</summary>
    public static byte[] Jpg(int width = 200, int height = 100, ushort? orientation = null)
    {
        var jpg = Encode(TwoColours(width, height), SKEncodedImageFormat.Jpeg);
        return orientation is { } value ? WithExifOrientation(jpg, value) : jpg;
    }

    public static byte[] Png(int width = 120, int height = 80) => Encode(TwoColours(width, height), SKEncodedImageFormat.Png);

    /// <summary>An A4 PDF with this many pages, drawn by Skia's PDF backend.</summary>
    public static byte[] Pdf(int pages)
    {
        using var output = new MemoryStream();
        using (var document = SKDocument.CreatePdf(output))
        {
            using var font = new SKFont(SKTypeface.Default, 24);
            using var paint = new SKPaint { Color = SKColors.Black };
            for (var page = 1; page <= pages; page++)
            {
                var canvas = document.BeginPage(595, 842);
                canvas.DrawText($"Page {page}", 72, 100, SKTextAlign.Left, font, paint);
                document.EndPage();
            }

            document.Close();
        }

        return output.ToArray();
    }

    /// <summary>A minimal but real Word file; each string is a paragraph and null is a page break.</summary>
    public static byte[] Docx(params string?[] paragraphs)
    {
        var body = string.Concat(paragraphs.Select(text => text is null
            ? """<w:p><w:r><w:br w:type="page"/></w:r></w:p>"""
            : $"""<w:p><w:r><w:t xml:space="preserve">{SecurityElement.Escape(text)}</w:t></w:r></w:p>"""));

        return Zip(
            ("[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
                </Types>
                """),
            ("_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
                </Relationships>
                """),
            ("word/document.xml", $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>{body}</w:body></w:document>
                """));
    }

    /// <summary>A minimal but real Excel file with one worksheet of text and whole numbers.</summary>
    public static byte[] Xlsx(string sheetName, params object[][] rows)
    {
        var sheetRows = new StringBuilder();
        for (var r = 0; r < rows.Length; r++)
        {
            sheetRows.Append($"""<row r="{r + 1}">""");
            for (var c = 0; c < rows[r].Length; c++)
            {
                var cell = $"{(char)('A' + c)}{r + 1}";
                sheetRows.Append(rows[r][c] is long or int
                    ? $"""<c r="{cell}"><v>{rows[r][c]}</v></c>"""
                    : $"""<c r="{cell}" t="inlineStr"><is><t>{SecurityElement.Escape(rows[r][c].ToString())}</t></is></c>""");
            }

            sheetRows.Append("</row>");
        }

        return Zip(
            ("[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """),
            ("_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """),
            ("xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """),
            ("xl/workbook.xml", $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="{SecurityElement.Escape(sheetName)}" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """),
            ("xl/worksheets/sheet1.xml", $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>{sheetRows}</sheetData></worksheet>
                """));
    }

    /// <summary>Text with a .docx name: what a renamed file looks like. Gotenberg would turn it into a PDF of garbage.</summary>
    public static byte[] FakeDocx() => Encoding.UTF8.GetBytes("Đây không phải là tệp Word. This is not a Word file.");

    /// <summary>A ZIP that is an Office package but neither a document nor a workbook.</summary>
    public static byte[] OfficePackageOfNeither() =>
        Zip(("[Content_Types].xml", """<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"/>"""), ("ppt/presentation.xml", "<p/>"));

    /// <summary>A ZIP that is not an Office package at all.</summary>
    public static byte[] PlainZip() => Zip(("word/document.xml", "<w:document/>"));

    public static byte[] Zip(params (string Name, string Content)[] entries)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var stream = zip.CreateEntry(name).Open();
                stream.Write(Encoding.UTF8.GetBytes(content));
            }
        }

        return output.ToArray();
    }

    /// <summary>Red on the left half, blue on the right, so a rotation can be seen in the pixels.</summary>
    private static SKBitmap TwoColours(int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        using var red = new SKPaint { Color = SKColors.Red };
        using var blue = new SKPaint { Color = SKColors.Blue };
        canvas.DrawRect(0, 0, width / 2f, height, red);
        canvas.DrawRect(width / 2f, 0, width / 2f, height, blue);
        return bitmap;
    }

    private static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format)
    {
        using (bitmap)
        using (var image = SKImage.FromBitmap(bitmap))
        using (var data = image.Encode(format, 95))
        {
            return data.ToArray();
        }
    }

    /// <summary>
    /// Puts an EXIF APP1 segment with one Orientation tag right after the JPEG's start marker, as a phone camera does.
    /// </summary>
    private static byte[] WithExifOrientation(byte[] jpg, ushort orientation)
    {
        byte[] exif =
        [
            0xFF, 0xE1, 0x00, 0x22,                         // APP1, 34 bytes including this length
            (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0,
            (byte)'I', (byte)'I', 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00, // little-endian TIFF, first IFD at 8
            0x01, 0x00,                                     // one entry
            0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00, // tag 0x0112 Orientation, SHORT, count 1
            (byte)orientation, 0x00, 0x00, 0x00,            // the value, padded to four bytes
            0x00, 0x00, 0x00, 0x00,                         // no next IFD
        ];
        return [.. jpg.AsSpan(0, 2), .. exif, .. jpg.AsSpan(2)];
    }
}
