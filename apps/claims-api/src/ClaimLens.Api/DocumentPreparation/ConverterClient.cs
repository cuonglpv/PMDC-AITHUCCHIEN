using System.Net.Http.Headers;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.DocumentStore;

namespace ClaimLens.Api.DocumentPreparation;

/// <summary>Turns a DOCX or XLSX file into a PDF rendition (US03, BR-02).</summary>
public interface IDocumentConverter
{
    /// <summary>The PDF. Throws <see cref="ConversionFailedException"/> when the converter refuses the file.</summary>
    Task<byte[]> ToPdfAsync(byte[] content, FileFormat format, CancellationToken cancel);
}

/// <summary>The converter answered, and the answer was not a PDF. The file is rejected with this reason.</summary>
public sealed class ConversionFailedException(string reason) : Exception(reason);

/// <summary>
/// Gotenberg's LibreOffice route. It picks the input format from the file name's extension, so the name sent is
/// generated from the detected type: the uploaded name never leaves the API.
/// </summary>
public sealed class ConverterClient(HttpClient http) : IDocumentConverter
{
    public const string Route = "forms/libreoffice/convert";

    public async Task<byte[]> ToPdfAsync(byte[] content, FileFormat format, CancellationToken cancel)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(StorageKeys.ContentType(format));
        form.Add(file, "files", $"document.{StorageKeys.Extension(format)}");

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync(Route, form, cancel);
        }
        catch (TaskCanceledException) when (!cancel.IsCancellationRequested)
        {
            throw new ConversionFailedException("The conversion to PDF took too long.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                // Gotenberg answers 400 for a file LibreOffice cannot open, and 503 when the conversion timed out.
                throw new ConversionFailedException((int)response.StatusCode == 503
                    ? "The conversion to PDF took too long."
                    : "The file could not be converted to PDF.");
            }

            var pdf = await response.Content.ReadAsByteArrayAsync(cancel);
            if (FileSignature.Detect(pdf) != FileFormat.Pdf)
            {
                throw new ConversionFailedException("The file could not be converted to PDF.");
            }

            return pdf;
        }
    }
}
