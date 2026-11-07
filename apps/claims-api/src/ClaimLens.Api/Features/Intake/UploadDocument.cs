using System.Security.Cryptography;
using ClaimLens.Api.AccessControl;
using ClaimLens.Api.Audit;
using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.DocumentPreparation;
using ClaimLens.Api.DocumentStore;
using ClaimLens.Api.Features.Evidence;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace ClaimLens.Api.Features.Intake;

/// <summary>
/// US03: one file per request. Size and type are checked at once (413, 415), the type by the file's first bytes. The
/// original is stored first and only then is the row inserted as CONVERTING, so a failed store leaves no row that would
/// block submission (docs/10-code-structure.md, section 4). Conversion and pages follow in the background.
/// </summary>
public static class UploadDocument
{
    /// <summary>Room for the multipart boundaries and headers around a file of the largest size.</summary>
    private const long EnvelopeBytes = 64 * 1024;

    private const int FileNameLength = 255;

    public static async Task<IResult> Handle(
        HttpContext context,
        ClaimLensDbContext db,
        IDocumentStore store,
        PreparationSignal preparation,
        IOptions<LimitsOptions> limits,
        TimeProvider clock,
        ILoggerFactory logs,
        CancellationToken cancel)
    {
        var maxBytes = limits.Value.MaxFileBytes;
        if (context.Request.ContentLength > maxBytes + EnvelopeBytes)
        {
            return TooLarge(context, maxBytes);
        }

        Upload? upload;
        try
        {
            upload = await ReadFileAsync(context.Request, maxBytes, cancel);
        }
        catch (BadHttpRequestException error) when (error.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return TooLarge(context, maxBytes);
        }
        catch (Exception error) when (error is IOException or InvalidDataException)
        {
            // A body that is not well-formed multipart is not a file we accept.
            upload = null;
        }

        if (upload is FileTooLarge)
        {
            return TooLarge(context, maxBytes);
        }

        if (upload is not ReceivedFile file || file.Content.Length == 0 || FileSignature.Detect(file.Content) is not { } format)
        {
            return Problems.Of(
                context,
                StatusCodes.Status415UnsupportedMediaType,
                ErrorCodes.UnsupportedFileType,
                "Upload a JPG, PNG, PDF, DOCX or XLSX file");
        }

        context.User.TryGetStaff(out var staffId, out _);
        var claimId = ClaimAccess.Of(context).ClaimId;
        var document = new Document
        {
            Id = Guid.CreateVersion7(),
            ClaimId = claimId,
            OriginalFilename = FileName(file.Name, format),
            FileFormat = format,
            SizeBytes = file.Content.Length,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(file.Content)),
            OriginalKey = "",
            Status = DocumentStatus.Converting,
            UploadedBy = staffId,
            UploadedAt = clock.GetUtcNow(),
        };
        document.OriginalKey = StorageKeys.Original(claimId, document.Id, format);

        await store.PutAsync(document.OriginalKey, file.Content, StorageKeys.ContentType(format), cancel);
        try
        {
            // Under the claim's lock, so a submission that started after the policy check cannot miss this file.
            await using var transaction = await db.Database.BeginTransactionAsync(cancel);
            var status = (await db.Claims
                    .FromSql($"select * from claim where id = {claimId} for update")
                    .AsNoTracking()
                    .ToListAsync(cancel))
                .Single()
                .Status;
            if (status != ClaimStatus.Draft)
            {
                await DiscardAsync(store, document, logs, cancel);
                return Problems.Of(context, StatusCodes.Status409Conflict, ErrorCodes.ClaimStateConflict, "The claim's status does not allow this");
            }

            db.Documents.Add(document);
            db.AuditEvents.Add(AuditLog.Event(
                context,
                ActorType.User,
                staffId,
                claimId,
                "DOCUMENT_UPLOADED",
                "document",
                document.Id.ToString(),
                after: new { document.FileFormat, document.SizeBytes, document.Sha256, document.Status }));
            await db.SaveChangesAsync(cancel);
            await transaction.CommitAsync(cancel);
        }
        catch
        {
            await DiscardAsync(store, document, logs, CancellationToken.None);
            throw;
        }

        preparation.Notify();
        return Results.Json(await DocumentResponses.OneAsync(db, document, cancel), statusCode: StatusCodes.Status201Created);
    }

    private abstract record Upload;

    private sealed record ReceivedFile(string? Name, byte[] Content) : Upload;

    private sealed record FileTooLarge : Upload;

    /// <summary>
    /// The `file` part of a multipart body, read into memory up to the limit. Memory rather than a temporary file,
    /// because a temporary file would put claim data on disk.
    /// </summary>
    private static async Task<Upload?> ReadFileAsync(HttpRequest request, long maxBytes, CancellationToken cancel)
    {
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var type)
            || !type.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase)
            || HeaderUtilities.RemoveQuotes(type.Boundary).Value is not { Length: > 0 } boundary)
        {
            return null;
        }

        var reader = new MultipartReader(boundary, request.Body);
        while (await reader.ReadNextSectionAsync(cancel) is { } section)
        {
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition)
                || !disposition.IsFileDisposition()
                || HeaderUtilities.RemoveQuotes(disposition.Name).Value != "file")
            {
                continue;
            }

            using var content = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await section.Body.ReadAsync(buffer, cancel)) > 0)
            {
                if (content.Length + read > maxBytes)
                {
                    return new FileTooLarge();
                }

                content.Write(buffer, 0, read);
            }

            var name = disposition.FileNameStar.HasValue ? disposition.FileNameStar.Value : HeaderUtilities.RemoveQuotes(disposition.FileName).Value;
            return new ReceivedFile(name, content.ToArray());
        }

        return null;
    }

    /// <summary>The name as the officer knows it, without a client path. It is shown, never used as a storage key.</summary>
    private static string FileName(string? raw, FileFormat format)
    {
        var name = (raw ?? "").Replace('\\', '/');
        name = new string(name[(name.LastIndexOf('/') + 1)..].Where(character => !char.IsControl(character)).ToArray()).Trim();
        if (name.Length == 0)
        {
            return $"upload.{StorageKeys.Extension(format)}";
        }

        if (name.Length <= FileNameLength)
        {
            return name;
        }

        var extension = Path.GetExtension(name);
        return extension.Length is > 0 and <= 10
            ? name[..(FileNameLength - extension.Length)] + extension
            : name[..FileNameLength];
    }

    private static IResult TooLarge(HttpContext context, long maxBytes) =>
        Problems.Of(
            context,
            StatusCodes.Status413PayloadTooLarge,
            ErrorCodes.FileTooLarge,
            $"A file can be at most {maxBytes / (1024 * 1024)} MB");

    private static async Task DiscardAsync(IDocumentStore store, Document document, ILoggerFactory logs, CancellationToken cancel)
    {
        try
        {
            await store.DeleteAsync([document.OriginalKey], cancel);
        }
        catch (Exception error)
        {
            logs.CreateLogger(typeof(UploadDocument)).LogWarning(error, "Could not delete the stored original of refused upload {DocumentId}", document.Id);
        }
    }
}
