using ClaimLens.Api.DocumentStore;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.Evidence;

/// <summary>
/// A page image, always through the API so each read is checked and audited (BR-11, BR-12). The page is looked up with
/// its claim id, so a page of claim B through /claims/A/... is not found. `variant=thumbnail` reads the small copy kept
/// under thumbnails/ beside the page.
/// </summary>
public static class GetPage
{
    public static async Task<IResult> Handle(
        HttpContext context,
        ClaimLensDbContext db,
        IDocumentStore store,
        string pageId,
        CancellationToken cancel)
    {
        var variant = context.Request.Query["variant"].ToString();
        if (variant is not ("" or "full" or "thumbnail") || !Guid.TryParse(pageId, out var page))
        {
            return Requests.NotFound(context);
        }

        var claimId = ClaimAccess.Of(context).ClaimId;
        var imageKey = await db.DocumentPages.AsNoTracking()
            .Where(row => row.Id == page && row.ClaimId == claimId)
            .Select(row => row.ImageKey)
            .FirstOrDefaultAsync(cancel);
        if (imageKey is null)
        {
            return Requests.NotFound(context);
        }

        var key = variant == "thumbnail" ? StorageKeys.ThumbnailOf(imageKey) : imageKey;
        var image = await store.GetAsync(key, cancel);
        if (image is null)
        {
            return Requests.NotFound(context);
        }

        // Claim data: no shared cache may keep it, and neither may the browser's disk cache, which would outlive the
        // sign-out on a shared workstation.
        context.Response.Headers.CacheControl = "private, no-store";
        return Results.File(image, "image/png");
    }
}
