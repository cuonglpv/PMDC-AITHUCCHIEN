namespace ClaimLens.Api.Features.Evidence;

public static class EvidenceEndpoints
{
    public static void MapEvidence(this IEndpointRouteBuilder api)
    {
        api.MapGet("/claims/{claimId}/documents", ListDocuments.Handle).WithName("listDocuments");
        api.MapGet("/claims/{claimId}/pages/{pageId}/image", GetPage.Handle).WithName("getPage");
        api.MapPut("/claims/{claimId}/pages/{pageId}/type", ReclassifyPage.Handle).WithName("reclassifyPage");
        api.MapPost("/claims/{claimId}/pages/{pageId}/rerun-extraction", RerunExtraction.Handle).WithName("rerunExtraction");
        api.MapGet("/claims/{claimId}/fields", ListFields.Handle).WithName("listFields");
        api.MapPatch("/claims/{claimId}/fields/{fieldId}", UpdateField.Handle).WithName("updateField");
        api.MapPost("/claims/{claimId}/fields/{fieldId}/accept", AcceptField.Handle).WithName("acceptField");
    }
}
