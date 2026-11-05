namespace ClaimLens.Api.Http;

/// <summary>
/// Maps every contract operation that has no handler yet, answering 501. The policy middleware still runs on them,
/// so the role, scope and status rules of all 59 operations are enforced and tested before their features exist.
/// This class shrinks to nothing as features are built.
/// </summary>
public static class NotBuiltEndpoints
{
    public static void MapRemaining(RouteGroupBuilder api, IEndpointRouteBuilder app)
    {
        var built = app.DataSources
            .SelectMany(source => source.Endpoints)
            .Concat(((IEndpointRouteBuilder)api).DataSources.SelectMany(source => source.Endpoints))
            .Select(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        foreach (var policy in OperationPolicies.All.Where(policy => !built.Contains(policy.OperationId)))
        {
            api.MapMethods(policy.Path, [policy.Method], () => Results.StatusCode(StatusCodes.Status501NotImplemented))
                .WithName(policy.OperationId);
        }
    }
}
