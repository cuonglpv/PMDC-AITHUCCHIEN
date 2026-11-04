using ClaimLens.Api.Domain.Enums;

namespace ClaimLens.Api.Http;

/// <summary>Which claims a role may reach through one operation (x-claimlens.roles).</summary>
public enum ClaimScopeKind
{
    Any,
    Registered,
    Assigned,
    All,
}

public enum ReleaseKind
{
    Must,
    Should,
}

public enum AuditKind
{
    None,
    Read,
    Write,
}

/// <summary>One row of the contract's x-claimlens data. An empty role list means the operation is public.</summary>
public sealed record OperationPolicy(
    string OperationId,
    string Method,
    string Path,
    IReadOnlyDictionary<StaffRole, ClaimScopeKind> Roles,
    IReadOnlyList<ClaimStatus>? States,
    ReleaseKind Release,
    AuditKind Audit)
{
    public bool IsPublic => Roles.Count == 0;
}

public static partial class OperationPolicies
{
    private static readonly Dictionary<string, OperationPolicy> ById =
        All.ToDictionary(policy => policy.OperationId, StringComparer.Ordinal);

    public static OperationPolicy? Find(string operationId) => ById.GetValueOrDefault(operationId);
}

public sealed class ReleaseOptions
{
    /// <summary>Off for the first release: Should operations answer 404 (docs/10-code-structure.md, section 11).</summary>
    public bool IncludeShould { get; set; }
}
