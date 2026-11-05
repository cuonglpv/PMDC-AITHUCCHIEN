using System.Net;
using ClaimLens.Api.Domain.Enums;

namespace ClaimLens.Api.Domain.Entities;

public sealed class StaffAccount
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string FullName { get; set; }
    public required string Initials { get; set; }
    public StaffRole Role { get; set; }
    public AccountStatus Status { get; set; }
    public DateTimeOffset? LastSignInAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class RefreshTokenRecord
{
    public Guid Id { get; set; }
    public Guid StaffAccountId { get; set; }
    public Guid FamilyId { get; set; }
    public required string TokenHash { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RotatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed class AuditEvent
{
    public long Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public ActorType ActorType { get; set; }
    public Guid? ActorId { get; set; }
    public Guid? ClaimId { get; set; }
    public required string EventType { get; set; }
    public required string EntityType { get; set; }
    public required string EntityId { get; set; }
    public string? BeforeValue { get; set; }
    public string? AfterValue { get; set; }
    public Guid? AiRunId { get; set; }
    public string? RequestId { get; set; }
    public IPAddress? IpAddress { get; set; }
}

/// <summary>The claim columns the access checks need. The other columns arrive with Claim Intake and Claim Lifecycle.</summary>
public sealed class Claim
{
    public Guid Id { get; set; }
    public ClaimStatus Status { get; set; }
    public Guid RegisteredBy { get; set; }
    public Guid? AssigneeId { get; set; }
}
