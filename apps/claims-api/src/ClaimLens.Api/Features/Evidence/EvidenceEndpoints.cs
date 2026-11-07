namespace ClaimLens.Api.Features.Evidence;

public static class EvidenceEndpoints
{
    public static void MapEvidence(this IEndpointRouteBuilder api)
    {
        api.MapGet("/claims/{claimId}/documents", ListDocuments.Handle).WithName("listDocuments");
        api.MapGet("/claims/{claimId}/pages/{pageId}/image", GetPage.Handle).WithName("getPage");
        api.MapPut("/claims/{claimId}/pages/{pageId}/type", ReclassifyPage.Handle).WithName("reclassifyPage");
    }
}
