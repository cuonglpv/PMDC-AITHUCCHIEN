using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;

namespace ClaimLens.Api.Features.Evidence;

/// <summary>Files and their pages, without removed ones. The middleware audits the read.</summary>
public static class ListDocuments
{
    public static async Task<IResult> Handle(HttpContext context, ClaimLensDbContext db, CancellationToken cancel) =>
        Results.Ok(new DocumentList(await DocumentResponses.ForClaimAsync(db, ClaimAccess.Of(context).ClaimId, cancel)));
}
