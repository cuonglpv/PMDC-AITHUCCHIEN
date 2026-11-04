namespace ClaimLens.Api.Http;

/// <summary>Every response has X-Request-Id, also stored in audit_event.request_id (docs/07-api-contract.md).</summary>
public sealed class RequestIdMiddleware(RequestDelegate next)
{
    private const string ItemKey = "ClaimLens.RequestId";

    public Task InvokeAsync(HttpContext context)
    {
        var id = Guid.CreateVersion7().ToString("N");
        context.Items[ItemKey] = id;
        context.Response.Headers["X-Request-Id"] = id;
        return next(context);
    }

    public static string Current(HttpContext context) =>
        context.Items[ItemKey] as string ?? context.TraceIdentifier;
}
