namespace ClaimLens.Api.Features.Intake;

public static class IntakeEndpoints
{
    public static void MapIntake(this IEndpointRouteBuilder api)
    {
        api.MapPost("/members/search", SearchMembers.Handle).WithName("searchMembers");
        api.MapGet("/policies/{policyId}/summary", GetPolicySummary.Handle).WithName("getPolicySummary");
        api.MapPost("/claims", CreateClaim.Handle).WithName("createClaim");
        api.MapPatch("/claims/{claimId}", UpdateClaim.Handle).WithName("updateClaim");
        api.MapPost("/claims/{claimId}/submit", SubmitClaim.Handle).WithName("submitClaim");
        api.MapPost("/claims/{claimId}/documents", UploadDocument.Handle).WithName("uploadDocument");
        api.MapDelete("/claims/{claimId}/documents/{documentId}", DeleteDocument.Handle).WithName("deleteDocument");
    }
}
