using System.Text.Json;
using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Http;

namespace ClaimLens.Api.Audit;

/// <summary>
/// Builds insert-only audit events (BR-12). The caller adds the event to its own DbContext, so it commits in the same
/// transaction as the change it describes. The database refuses UPDATE and DELETE from the app's role.
/// </summary>
public static class AuditLog
{
    /// <summary>The API's own JSON rules, so an enum reads `READY` here as it does in the database and the API.</summary>
    private static readonly JsonSerializerOptions Json = CreateJson();

    private static JsonSerializerOptions CreateJson()
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Startup.ConfigureJson(json);
        return json;
    }

    public static AuditEvent Event(
        HttpContext context,
        ActorType actorType,
        Guid? actorId,
        Guid? claimId,
        string eventType,
        string entityType,
        string entityId,
        object? before = null,
        object? after = null) =>
        new()
        {
            OccurredAt = context.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow(),
            ActorType = actorType,
            ActorId = actorId,
            ClaimId = claimId,
            EventType = eventType,
            EntityType = entityType,
            EntityId = entityId,
            BeforeValue = before is null ? null : JsonSerializer.Serialize(before, Json),
            AfterValue = after is null ? null : JsonSerializer.Serialize(after, Json),
            RequestId = RequestIdMiddleware.Current(context),
            IpAddress = context.Connection.RemoteIpAddress,
        };

    /// <summary>A change the system made outside any request, such as a background worker's. No person, no address.</summary>
    public static AuditEvent SystemEvent(
        TimeProvider clock,
        Guid? claimId,
        string eventType,
        string entityType,
        string entityId,
        object? before = null,
        object? after = null) =>
        new()
        {
            OccurredAt = clock.GetUtcNow(),
            ActorType = ActorType.System,
            ClaimId = claimId,
            EventType = eventType,
            EntityType = entityType,
            EntityId = entityId,
            BeforeValue = before is null ? null : JsonSerializer.Serialize(before, Json),
            AfterValue = after is null ? null : JsonSerializer.Serialize(after, Json),
        };
}
