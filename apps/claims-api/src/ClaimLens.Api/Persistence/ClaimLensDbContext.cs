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
    public DbSet<BenefitPlan> BenefitPlans => Set<BenefitPlan>();
    public DbSet<RuleSetVersion> RuleSetVersions => Set<RuleSetVersion>();
    public DbSet<BenefitCategory> BenefitCategories => Set<BenefitCategory>();
    public DbSet<BenefitRule> BenefitRules => Set<BenefitRule>();

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

        modelBuilder.Entity<BenefitPlan>(table =>
        {
            table.ToTable("benefit_plan");
            table.Property(row => row.Id).HasColumnName("id");
            table.Property(row => row.Code).HasColumnName("code");
            table.Property(row => row.Name).HasColumnName("name");
            table.Property(row => row.Description).HasColumnName("description");
            table.Property(row => row.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<RuleSetVersion>(table =>
        {
            table.ToTable("rule_set_version");
            table.Property(row => row.Id).HasColumnName("id");
            table.Property(row => row.PlanId).HasColumnName("plan_id");
            table.Property(row => row.VersionNo).HasColumnName("version_no");
            table.Property(row => row.Status).HasColumnName("status");
            table.Property(row => row.EffectiveFrom).HasColumnName("effective_from");
            table.Property(row => row.BasedOnVersionId).HasColumnName("based_on_version_id");
            table.Property(row => row.ChangeNote).HasColumnName("change_note");
            table.Property(row => row.CreatedBy).HasColumnName("created_by");
            table.Property(row => row.CreatedAt).HasColumnName("created_at");
            table.Property(row => row.PublishedBy).HasColumnName("published_by");
            table.Property(row => row.PublishedAt).HasColumnName("published_at");
            table.HasMany<BenefitRule>().WithOne().HasForeignKey(rule => rule.RuleSetVersionId);
        });

        modelBuilder.Entity<BenefitCategory>(table =>
        {
            table.ToTable("benefit_category");
            table.HasKey(row => row.Code);
            table.Property(row => row.Code).HasColumnName("code");
            table.Property(row => row.Name).HasColumnName("name");
            table.Property(row => row.Description).HasColumnName("description");
            table.Property(row => row.IsActive).HasColumnName("is_active");
        });

        modelBuilder.Entity<BenefitRule>(table =>
        {
            table.ToTable("benefit_rule");
            table.Property(row => row.Id).HasColumnName("id");
            table.Property(row => row.RuleSetVersionId).HasColumnName("rule_set_version_id");
            table.Property(row => row.RuleCode).HasColumnName("rule_code");
            table.Property(row => row.RuleType).HasColumnName("rule_type");
            table.Property(row => row.CategoryCode).HasColumnName("category_code");
            table.Property(row => row.ClauseText).HasColumnName("clause_text");
            table.Property(row => row.ClauseRef).HasColumnName("clause_ref");
            table.Property(row => row.ApplyOrder).HasColumnName("apply_order");
            table.Property(row => row.RateBp).HasColumnName("rate_bp");
            table.Property(row => row.LimitAmount).HasColumnName("limit_amount");
            table.Property(row => row.LimitScope).HasColumnName("limit_scope");
            table.Property(row => row.WaitingDays).HasColumnName("waiting_days");
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
