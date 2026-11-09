using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Features.Lifecycle;
using ClaimLens.Api.Features.RuleSets;

namespace ClaimLens.Api.Features.Adjudication;

/// <summary>A line's benefit category. Mapping arrives with US10; until then every line is PENDING with none of the rest.</summary>
public sealed record LineMapping(MappingStatus Status, string? CategoryCode, object? Ai, StaffRef? ResolvedBy, DateTimeOffset? ResolvedAt);

/// <summary>`override` arrives with US16.</summary>
public sealed record ExpenseLineResponse(
    Guid Id,
    Guid DocumentId,
    int LineNo,
    string? Description,
    decimal? Quantity,
    long? UnitPrice,
    long? Amount,
    LineOrigin Origin,
    LineMapping Mapping,
    object? Override);

public sealed record ExpenseLineList(IReadOnlyList<ExpenseLineResponse> Items);

public sealed record LineChangeResult(ExpenseLineResponse Line, ClaimProgress Progress);

public static class ExpenseLineResponses
{
    public static ExpenseLineResponse Of(ExpenseLine line) =>
        new(
            line.Id,
            line.DocumentId,
            line.LineNo,
            line.Description,
            line.Quantity,
            line.UnitPrice,
            line.Amount,
            line.Origin,
            new LineMapping(line.MappingStatus, null, null, null, null),
            null);
}
