using ClaimLens.Api.AccessControl;
using ClaimLens.Api.Audit;
using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Persistence;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClaimLens.Api.Http;

/// <summary>
/// Enforces x-claimlens on every operation. It is middleware rather than an endpoint filter so it runs before
/// model binding: a caller who may not use an operation never learns anything from its validation errors.
/// Order: release, role, scope, status, handler, read audit (docs/10-code-structure.md, section 4). A read's body is
/// held back until its audit event is saved.
/// </summary>
public sealed class OperationPolicyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ClaimLensDbContext db, IOptions<ReleaseOptions> release)
    {
        var name = context.GetEndpoint()?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName;
        var policy = name is null ? null : OperationPolicies.Find(name);
        if (policy is null)
        {
            await next(context);
            return;
        }

        var refusal = await CheckAsync(context, db, policy, release.Value);
        if (refusal is not null)
        {
            await refusal.ExecuteAsync(context);
            return;
        }

        if (policy.Audit != AuditKind.Read)
        {
            await next(context);
            return;
        }

        // The data of a read leaves only after its audit event is saved (BR-12). The handler writes into a buffer, so a
        // failed insert still lets ErrorMiddleware answer with an error instead of the claim's data. getPage is an
        // image of at most one page, so memory is enough; a temporary file would put claim data on disk.
        var server = context.Features.GetRequiredFeature<IHttpResponseBodyFeature>();
        using var buffer = new MemoryStream();
        var buffering = new StreamResponseBodyFeature(buffer, server);
        context.Features.Set<IHttpResponseBodyFeature>(buffering);
        try
        {
            await next(context);
            await buffering.CompleteAsync();
        }
        finally
        {
            context.Features.Set(server);
        }

        if (context.Response.StatusCode is >= 200 and < 300)
        {
            await RecordReadAsync(context, db, policy);
        }

        buffer.Position = 0;
        await buffer.CopyToAsync(context.Response.Body, context.RequestAborted);
    }

    private static async Task<IResult?> CheckAsync(
        HttpContext context,
        ClaimLensDbContext db,
        OperationPolicy policy,
        ReleaseOptions release)
    {
        if (policy.Release == ReleaseKind.Should && !release.IncludeShould)
        {
            return Problems.Of(context, StatusCodes.Status404NotFound, ErrorCodes.NotFound, "Not found");
        }

        if (policy.IsPublic)
        {
            return null;
        }

        if (!context.User.TryGetStaff(out var staffId, out var role))
        {
            return Problems.Of(context, StatusCodes.Status401Unauthorized, ErrorCodes.AuthRequired, "Sign in to continue");
        }

        if (!policy.Roles.TryGetValue(role, out var scope))
        {
            return Problems.Of(context, StatusCodes.Status403Forbidden, ErrorCodes.ForbiddenRole, "Your role cannot do this");
        }

        if (!context.Request.RouteValues.TryGetValue("claimId", out var raw))
        {
            return null;
        }

        if (!Guid.TryParse(raw?.ToString(), out var claimId))
        {
            return Problems.Of(context, StatusCodes.Status404NotFound, ErrorCodes.NotFound, "Not found");
        }

        var claim = await db.Claims.AsNoTracking()
            .Where(row => row.Id == claimId)
            .Select(row => new ClaimAccess(row.Id, row.Status, row.RegisteredBy, row.AssigneeId))
            .FirstOrDefaultAsync(context.RequestAborted);

        if (claim is null)
        {
            return Problems.Of(context, StatusCodes.Status404NotFound, ErrorCodes.NotFound, "Not found");
        }

        if (!claim.InScope(scope, staffId))
        {
            return Problems.Of(context, StatusCodes.Status403Forbidden, ErrorCodes.ClaimOutOfScope, "This claim is not available to you");
        }

        if (policy.States is { } states && !states.Contains(claim.Status))
        {
            return Problems.Of(context, StatusCodes.Status409Conflict, ErrorCodes.ClaimStateConflict, "The claim's status does not allow this");
        }

        // Handlers reuse this instead of loading the claim again.
        context.Items[ClaimAccess.ItemKey] = claim;
        return null;
    }

    private static async Task RecordReadAsync(HttpContext context, ClaimLensDbContext db, OperationPolicy policy)
    {
        context.User.TryGetStaff(out var staffId, out _);
        var claim = context.Items[ClaimAccess.ItemKey] as ClaimAccess;

        // A failed audit insert fails the request: no read of claim data goes unrecorded (BR-12).
        db.AuditEvents.Add(AuditLog.Event(
            context,
            ActorType.User,
            staffId,
            claim?.ClaimId,
            claim is null ? "DATA_READ" : "CLAIM_VIEWED",
            claim is null ? "operation" : "claim",
            claim?.ClaimId.ToString() ?? policy.OperationId,
            after: new { operationId = policy.OperationId }));
        await db.SaveChangesAsync(context.RequestAborted);
    }
}

/// <summary>What the access checks needed from the claim row.</summary>
public sealed record ClaimAccess(Guid ClaimId, ClaimStatus Status, Guid RegisteredBy, Guid? AssigneeId)
{
    public const string ItemKey = "ClaimLens.ClaimAccess";

    /// <summary>The claim the middleware checked. Only for operations with a claimId in the route.</summary>
    public static ClaimAccess Of(HttpContext context) =>
        context.Items[ItemKey] as ClaimAccess
        ?? throw new InvalidOperationException("This operation has no claim the policy middleware checked.");

    public bool InScope(ClaimScopeKind scope, Guid staffId) =>
        scope switch
        {
            ClaimScopeKind.Registered => RegisteredBy == staffId,
            ClaimScopeKind.Assigned => AssigneeId == staffId,
            _ => true,
        };
}
