namespace ClaimLens.Api.Features.Adjudication;

public static class AdjudicationEndpoints
{
    public static void MapAdjudication(this IEndpointRouteBuilder api)
    {
        api.MapGet("/claims/{claimId}/expense-lines", ListExpenseLines.Handle).WithName("listExpenseLines");
        api.MapPost("/claims/{claimId}/expense-lines", AddExpenseLine.Handle).WithName("addExpenseLine");
        api.MapDelete("/claims/{claimId}/expense-lines/{lineId}", RemoveExpenseLine.Handle).WithName("removeExpenseLine");
    }
}
