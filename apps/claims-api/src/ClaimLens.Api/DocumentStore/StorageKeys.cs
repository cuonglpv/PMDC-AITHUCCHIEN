using System.Globalization;
using ClaimLens.Api.Domain.Enums;

namespace ClaimLens.Api.DocumentStore;

/// <summary>
/// Object keys from docs/10-code-structure.md, section 8. They are built from ids only and never contain a file name,
/// because a file name can hold a patient's name.
/// </summary>
public static class StorageKeys
{
    public const string PagesPrefix = "pages/";
    public const string ThumbnailsPrefix = "thumbnails/";

    public static string Original(Guid claimId, Guid documentId, FileFormat format) =>
        $"originals/{claimId}/{documentId}.{Extension(format)}";

    public static string Rendition(Guid claimId, Guid documentId) => $"renditions/{claimId}/{documentId}.pdf";

    public static string Page(Guid claimId, Guid documentId, int pageNo) =>
        string.Create(CultureInfo.InvariantCulture, $"{PagesPrefix}{claimId}/{documentId}/{pageNo}.png");

    /// <summary>The thumbnail sits beside the page under its own prefix, so `image_key` is enough to find both.</summary>
    public static string ThumbnailOf(string imageKey) =>
        imageKey.StartsWith(PagesPrefix, StringComparison.Ordinal)
            ? ThumbnailsPrefix + imageKey[PagesPrefix.Length..]
            : throw new ArgumentException("A page image key starts with pages/.", nameof(imageKey));

    public static string Extension(FileFormat format) => format.ToString().ToLowerInvariant();

    public static string ContentType(FileFormat format) =>
        format switch
        {
            FileFormat.Jpg => "image/jpeg",
            FileFormat.Png => "image/png",
            FileFormat.Pdf => "application/pdf",
            FileFormat.Docx => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            FileFormat.Xlsx => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream",
        };
}
