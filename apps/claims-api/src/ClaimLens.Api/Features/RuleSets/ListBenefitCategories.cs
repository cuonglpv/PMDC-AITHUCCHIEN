using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Features.RuleSets;

public static class ListBenefitCategories
{
    private sealed record CategoryResponse(string Code, string Name, string? Description);

    public static async Task<IResult> Handle(ClaimLensDbContext db, CancellationToken cancel)
    {
        var items = await db.BenefitCategories.AsNoTracking()
            .Where(category => category.IsActive)
            .OrderBy(category => category.Name)
            .Select(category => new CategoryResponse(category.Code, category.Name, category.Description))
            .ToListAsync(cancel);

        return Results.Ok(new Items<CategoryResponse>(items));
    }
}
