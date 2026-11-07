using ClaimLens.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaimLens.Api.Persistence;

/// <summary>Maps the tables that db/migrations creates. `dotnet ef migrations` is not used (docs/10-code-structure.md, section 7).</summary>
public sealed class ClaimLensDbContext(DbContextOptions<ClaimLensDbContext> options) : DbContext(options)
{
    public DbSet<StaffAccount> StaffAccounts => Set<StaffAccount>();
    public DbSet<RefreshTokenRecord> RefreshTokens => Set<RefreshTokenRecord>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentPage> DocumentPages => Set<DocumentPage>();
    public DbSet<AiRun> AiRuns => Set<AiRun>();
    public DbSet<BenefitPlan> BenefitPlans => Set<BenefitPlan>();
    public DbSet<RuleSetVersion> RuleSetVersions => Set<RuleSetVersion>();
    public DbSet<BenefitCategory> BenefitCategories => Set<BenefitCategory>();
    public DbSet<BenefitRule> BenefitRules => Set<BenefitRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDbFunction(typeof(Search).GetMethod(nameof(Search.Unaccent))!)
            .HasName("immutable_unaccent")
            .HasSchema("public");

        MapRelationships(modelBuilder);

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

        modelBuilder.Entity<Member>(table =>
        {
            table.ToTable("member");
            table.Property(row => row.Id).HasColumnName("id");
            table.Property(row => row.MemberCode).HasColumnName("member_code");
            table.Property(row => row.FullName).HasColumnName("full_name");
            table.Property(row => row.NationalId).HasColumnName("national_id");
            table.Property(row => row.DateOfBirth).HasColumnName("date_of_birth");
            table.Property(row => row.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<Policy>(table =>
        {
            table.ToTable("policy");
            table.Property(row => row.Id).HasColumnName("id");
            table.Property(row => row.PolicyNumber).HasColumnName("policy_number");
            table.Property(row => row.MemberId).HasColumnName("member_id");
            table.Property(row => row.PlanId).HasColumnName("plan_id");
            table.Property(row => row.Status).HasColumnName("status");
            table.Property(row => row.CoverageStart).HasColumnName("coverage_start");
            table.Property(row => row.CoverageEnd).HasColumnName("coverage_end");
            table.Property(row => row.FirstEnrolledOn).HasColumnName("first_enrolled_on");
            table.Property(row => row.TerminatedOn).HasColumnName("terminated_on");
            table.Property(row => row.OpeningConsumed).HasColumnName("opening_consumed");
            table.Property(row => row.ConsumedAmount).HasColumnName("consumed_amount");
            table.Property(row => row.BalanceVersion).HasColumnName("balance_version");
            table.Property(row => row.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<Claim>(table =>
        {
            table.ToTable("claim");
            table.Property(row => row.Id).HasColumnName("id");
            table.Property(row => row.ClaimNumber).HasColumnName("claim_number");
            table.Property(row => row.MemberId).HasColumnName("member_id");
            table.Property(row => row.PolicyId).HasColumnName("policy_id");
            table.Property(row => row.ProviderName).HasColumnName("provider_name");
            table.Property(row => row.ServiceDate).HasColumnName("service_date");
            table.Property(row => row.ClaimedTotal).HasColumnName("claimed_total");
            table.Property(row => row.Channel).HasColumnName("channel");
            table.Property(row => row.Status).HasColumnName("status");
            table.Property(row => row.StatusChangedAt).HasColumnName("status_changed_at");
            table.Property(row => row.RegisteredBy).HasColumnName("registered_by");
            table.Property(row => row.AssigneeId).HasColumnName("assignee_id");
            table.Property(row => row.AssignedAt).HasColumnName("assigned_at");
            table.Property(row => row.SubmittedAt).HasColumnName("submitted_at");
            table.Property(row => row.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
            table.Property(row => row.CreatedAt).HasColumnName("created_at");
            table.Property(row => row.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<Document>(table =>
        {
            table.ToTable("document");
            table.Property(row => row.Id).HasColumnName("id");
            table.Property(row => row.ClaimId).HasColumnName("claim_id");
            table.Property(row => row.OriginalFilename).HasColumnName("original_filename");
            table.Property(row => row.FileFormat).HasColumnName("file_format");
            table.Property(row => row.SizeBytes).HasColumnName("size_bytes");
            table.Property(row => row.Sha256).HasColumnName("sha256").IsFixedLength();
            table.Property(row => row.OriginalKey).HasColumnName("original_key");
            table.Property(row => row.RenditionKey).HasColumnName("rendition_key");
            table.Property(row => row.PageCount).HasColumnName("page_count");
            table.Property(row => row.Status).HasColumnName("status");
            table.Property(row => row.RejectionReason).HasColumnName("rejection_reason");
            table.Property(row => row.UploadedBy).HasColumnName("uploaded_by");
            table.Property(row => row.UploadedAt).HasColumnName("uploaded_at");
            table.Property(row => row.RemovedBy).HasColumnName("removed_by");
            table.Property(row => row.RemovedAt).HasColumnName("removed_at");
        });

        modelBuilder.Entity<DocumentPage>(table =>
        {
            table.ToTable("document_page");
            table.Property(row => row.Id).HasColumnName("id");
            table.Property(row => row.DocumentId).HasColumnName("document_id");
            table.Property(row => row.ClaimId).HasColumnName("claim_id");
            table.Property(row => row.PageNo).HasColumnName("page_no");
            table.Property(row => row.ClaimPageNo).HasColumnName("claim_page_no");
            table.Property(row => row.ImageKey).HasColumnName("image_key");
            table.Property(row => row.WidthPx).HasColumnName("width_px");
            table.Property(row => row.HeightPx).HasColumnName("height_px");
            table.Property(row => row.PageType).HasColumnName("page_type");
            table.Property(row => row.PageTypeSource).HasColumnName("page_type_source");
            table.Property(row => row.PageTypeSetBy).HasColumnName("page_type_set_by");
            table.Property(row => row.Quality).HasColumnName("quality");
            table.Property(row => row.AiStatus).HasColumnName("ai_status");
            table.Property(row => row.AiRunId).HasColumnName("ai_run_id");
            table.Property(row => row.AiPageType).HasColumnName("ai_page_type");
            table.Property(row => row.AiTypeConfidence).HasColumnName("ai_type_confidence").HasPrecision(4, 3);
            table.Property(row => row.AiTypeLevel).HasColumnName("ai_type_level");
            table.Property(row => row.AiQualityScore).HasColumnName("ai_quality_score").HasPrecision(4, 3);
        });

        modelBuilder.Entity<AiRun>(table =>
        {
            table.ToTable("ai_run");
            table.Property(row => row.Id).HasColumnName("id");
            table.Property(row => row.ClaimId).HasColumnName("claim_id");
            table.Property(row => row.Task).HasColumnName("task");
            table.Property(row => row.ScopeDocumentId).HasColumnName("scope_document_id");
            table.Property(row => row.ScopePageId).HasColumnName("scope_page_id");
            table.Property(row => row.ModelName).HasColumnName("model_name");
            table.Property(row => row.ModelVersion).HasColumnName("model_version");
            table.Property(row => row.Status).HasColumnName("status");
            table.Property(row => row.ErrorCode).HasColumnName("error_code");
            table.Property(row => row.TriggeredBy).HasColumnName("triggered_by");
            table.Property(row => row.RequestedAt).HasColumnName("requested_at");
            table.Property(row => row.StartedAt).HasColumnName("started_at");
            table.Property(row => row.FinishedAt).HasColumnName("finished_at");
            table.Property(row => row.RawOutputKey).HasColumnName("raw_output_key");
        });
    }

    /// <summary>
    /// The foreign keys between rows one SaveChanges inserts together, with no navigations. EF orders the inserts by
    /// them: a claim before its audit event, a run before the pages and the event that name it. The database holds the
    /// real constraints, and nothing is deleted through these.
    /// </summary>
    private static void MapRelationships(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Policy>().HasOne<Member>().WithMany().HasForeignKey(row => row.MemberId).OnDelete(DeleteBehavior.ClientNoAction);
        modelBuilder.Entity<Claim>().HasOne<Member>().WithMany().HasForeignKey(row => row.MemberId).OnDelete(DeleteBehavior.ClientNoAction);
        modelBuilder.Entity<Claim>().HasOne<Policy>().WithMany().HasForeignKey(row => row.PolicyId).OnDelete(DeleteBehavior.ClientNoAction);
        modelBuilder.Entity<Document>().HasOne<Claim>().WithMany().HasForeignKey(row => row.ClaimId).OnDelete(DeleteBehavior.ClientNoAction);
        modelBuilder.Entity<DocumentPage>().HasOne<Document>().WithMany().HasForeignKey(row => row.DocumentId).OnDelete(DeleteBehavior.ClientNoAction);
        modelBuilder.Entity<DocumentPage>().HasOne<Claim>().WithMany().HasForeignKey(row => row.ClaimId).OnDelete(DeleteBehavior.ClientNoAction);
        modelBuilder.Entity<DocumentPage>().HasOne<AiRun>().WithMany().HasForeignKey(row => row.AiRunId).OnDelete(DeleteBehavior.ClientNoAction);
        modelBuilder.Entity<AiRun>().HasOne<Claim>().WithMany().HasForeignKey(row => row.ClaimId).OnDelete(DeleteBehavior.ClientNoAction);
        modelBuilder.Entity<AiRun>().HasOne<Document>().WithMany().HasForeignKey(row => row.ScopeDocumentId).OnDelete(DeleteBehavior.ClientNoAction);
        modelBuilder.Entity<AuditEvent>().HasOne<Claim>().WithMany().HasForeignKey(row => row.ClaimId).OnDelete(DeleteBehavior.ClientNoAction);
        modelBuilder.Entity<AuditEvent>().HasOne<AiRun>().WithMany().HasForeignKey(row => row.AiRunId).OnDelete(DeleteBehavior.ClientNoAction);
    }
}
