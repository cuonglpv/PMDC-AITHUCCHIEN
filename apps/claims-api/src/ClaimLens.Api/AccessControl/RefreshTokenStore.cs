using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClaimLens.Api.AccessControl;

/// <summary>Issues, rotates and revokes refresh tokens (docs/07-api-contract.md, Sign-in and tokens).</summary>
public sealed class RefreshTokenStore(ClaimLensDbContext db, IOptions<AuthOptions> options, TimeProvider clock)
{
    public sealed record Issued(string Token, DateTimeOffset ExpiresAt);

    public Issued IssueForSignIn(StaffAccount staff)
    {
        var now = clock.GetUtcNow();
        return Add(staff.Id, Guid.CreateVersion7(), now, now.AddHours(options.Value.RefreshTokenHours));
    }

    /// <summary>Replaces a valid token. Returns null for anything else, revoking the family when a token is reused too late.</summary>
    public async Task<(StaffAccount Staff, Issued Token)?> RotateAsync(string presented, CancellationToken cancel)
    {
        var now = clock.GetUtcNow();
        var hash = TokenService.HashRefreshToken(presented);
        var row = await db.RefreshTokens.FirstOrDefaultAsync(token => token.TokenHash == hash, cancel);

        if (row is null || row.RevokedAt is not null || row.ExpiresAt <= now)
        {
            return null;
        }

        var grace = TimeSpan.FromSeconds(options.Value.RotationGraceSeconds);
        if (row.RotatedAt is { } rotated && now - rotated > grace)
        {
            // Reuse after the grace period suggests the token was stolen.
            await RevokeFamilyAsync(row.FamilyId, cancel);
            return null;
        }

        var staff = await db.StaffAccounts.FirstOrDefaultAsync(account => account.Id == row.StaffAccountId, cancel);
        if (staff is null || staff.Status != Domain.Enums.AccountStatus.Active)
        {
            await RevokeFamilyAsync(row.FamilyId, cancel);
            return null;
        }

        row.RotatedAt ??= now;

        // Rotation copies the expiry and never extends it.
        var issued = Add(staff.Id, row.FamilyId, now, row.ExpiresAt);
        await db.SaveChangesAsync(cancel);
        return (staff, issued);
    }

    /// <summary>Ends the sign-in the token belongs to, and returns who that was. Safe to repeat.</summary>
    public async Task<Guid?> RevokeAsync(string? presented, CancellationToken cancel)
    {
        if (string.IsNullOrEmpty(presented))
        {
            return null;
        }

        var hash = TokenService.HashRefreshToken(presented);
        var row = await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(token => token.TokenHash == hash, cancel);
        if (row is null)
        {
            return null;
        }

        await RevokeFamilyAsync(row.FamilyId, cancel);
        return row.StaffAccountId;
    }

    private Issued Add(Guid staffId, Guid familyId, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        var token = TokenService.NewRefreshToken();
        db.RefreshTokens.Add(new RefreshTokenRecord
        {
            Id = Guid.CreateVersion7(),
            StaffAccountId = staffId,
            FamilyId = familyId,
            TokenHash = TokenService.HashRefreshToken(token),
            IssuedAt = now,
            ExpiresAt = expiresAt,
        });
        return new Issued(token, expiresAt);
    }

    private async Task RevokeFamilyAsync(Guid familyId, CancellationToken cancel)
    {
        var now = clock.GetUtcNow();
        await db.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(token => token.RevokedAt, now), cancel);
    }
}
