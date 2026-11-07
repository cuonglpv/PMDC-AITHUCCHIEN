namespace ClaimLens.Api.Features.RuleSets;

public static class RuleSetEndpoints
{
    public static void MapRuleSets(this IEndpointRouteBuilder api)
    {
        api.MapGet("/plans", ListPlans.Handle).WithName("listPlans");
        api.MapGet("/plans/{planId}/versions", ListPlanVersions.Handle).WithName("listPlanVersions");
        api.MapPost("/plans/{planId}/versions", CreatePlanVersion.Handle).WithName("createPlanVersion");
        api.MapGet("/plans/{planId}/versions/{versionId}", GetPlanVersion.Handle).WithName("getPlanVersion");
        api.MapPut("/plans/{planId}/versions/{versionId}/rules", UpdateRuleSet.Handle).WithName("updateRuleSet");
        api.MapPost("/plans/{planId}/versions/{versionId}/publish", PublishPlanVersion.Handle).WithName("publishPlanVersion");
        api.MapPost("/plans/{planId}/versions/{versionId}/discard", DiscardPlanVersion.Handle).WithName("discardPlanVersion");
        api.MapGet("/benefit-categories", ListBenefitCategories.Handle).WithName("listBenefitCategories");
    }
}
