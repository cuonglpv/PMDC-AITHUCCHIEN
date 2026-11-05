using ClaimLens.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Persistence;

/// <summary>Maps the tables that db/migrations creates. `dotnet ef migrations` is not used (docs/10-code-structure.md, section 7).</summary>
public sealed class ClaimLensDbContext(DbContextOptions<ClaimLensDbContext> options) : DbContext(options)
{
    public DbSet<StaffAccount> StaffAccounts => Set<StaffAccount>();
    public DbSet<RefreshTokenRecord> RefreshTokens => Set<RefreshTokenRecord>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<Claim> Claims => Set<Claim>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StaffAccount>(table =>
        {
            table.ToTable("staff_account");
            table.Property(row => row.Id).HasColumnName("id");
            table.Property(row => row.Email).HasColumnName("email");
            table.Property(row => row.PasswordHash).HasColumnName("password_hash");
            table.Property(row => row.FullName).HasColumnName("full_name");
            table.Property(row => row.Initials).HasColumnName("initials");
            table.Property(row => row.Role).HasColumnName("role");
            table.Property(row => row.Status).HasColumnName("status");
            table.Property(row => row.LastSignInAt).HasColumnName("last_sign_in_at");
            table.Property(row => row.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<RefreshTokenRecord>(table =>
        {
            table.ToTable("refresh_token");
            table.Property(row => row.Id).HasColumnName("id");
            table.Property(row => row.StaffAccountId).HasColumnName("staff_account_id");
            table.Property(row => row.FamilyId).HasColumnName("family_id");
            table.Property(row => row.TokenHash).HasColumnName("token_hash");
            table.Property(row => row.IssuedAt).HasColumnName("issued_at");
            table.Property(row => row.ExpiresAt).HasColumnName("expires_at");
            table.Property(row => row.RotatedAt).HasColumnName("rotated_at");
            table.Property(row => row.RevokedAt).HasColumnName("revoked_at");
        });

        modelBuilder.Entity<AuditEvent>(table =>
        {
            table.ToTable("audit_event");
            table.Property(row => row.Id).HasColumnName("id");
            table.Property(row => row.OccurredAt).HasColumnName("occurred_at");
            table.Property(row => row.ActorType).HasColumnName("actor_type");
            table.Property(row => row.ActorId).HasColumnName("actor_id");
            table.Property(row => row.ClaimId).HasColumnName("claim_id");
            table.Property(row => row.EventType).HasColumnName("event_type");
            table.Property(row => row.EntityType).HasColumnName("entity_type");
            table.Property(row => row.EntityId).HasColumnName("entity_id");
            table.Property(row => row.BeforeValue).HasColumnName("before_value").HasColumnType("jsonb");
            table.Property(row => row.AfterValue).HasColumnName("after_value").HasColumnType("jsonb");
            table.Property(row => row.AiRunId).HasColumnName("ai_run_id");
            table.Property(row => row.RequestId).HasColumnName("request_id");
            table.Property(row => row.IpAddress).HasColumnName("ip_address");
        });

        modelBuilder.Entity<Claim>(table =>
        {
            table.ToTable("claim");
            table.Property(row => row.Id).HasColumnName("id");
            table.Property(row => row.Status).HasColumnName("status");
            table.Property(row => row.RegisteredBy).HasColumnName("registered_by");
            table.Property(row => row.AssigneeId).HasColumnName("assignee_id");
        });
    }
}
