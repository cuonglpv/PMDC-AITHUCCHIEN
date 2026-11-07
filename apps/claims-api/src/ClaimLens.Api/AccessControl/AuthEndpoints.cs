using ClaimLens.Api.Audit;
using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClaimLens.Api.AccessControl;

public static class AuthEndpoints
{
    public const string CookieName = "claimlens_refresh";
    private const string CookiePath = "/api/v1/auth";

    private sealed record LoginRequest(string? Email, string? Password);

    public sealed record StaffUser(Guid Id, string Email, string FullName, string Initials, StaffRole Role);

    public sealed record AuthSession(string AccessToken, string TokenType, int ExpiresIn, StaffUser User);

    public static void MapAuth(this IEndpointRouteBuilder api)
    {
        api.MapPost("/auth/login", Login).WithName("login");
        api.MapPost("/auth/refresh", Refresh).WithName("refreshToken");
        api.MapPost("/auth/logout", Logout).WithName("logout");
        api.MapGet("/me", GetMe).WithName("getMe");
    }

    private static async Task<IResult> Login(
        HttpContext context,
        ClaimLensDbContext db,
        IPasswordHasher<StaffAccount> hasher,
        TokenService tokens,
        RefreshTokenStore refresh,
        SignInThrottle throttle,
        TimeProvider clock)
    {
        var request = await Requests.ReadJsonAsync<LoginRequest>(context);
        var invalid = Validate(request);
        if (invalid.Count > 0)
        {
            return Problems.Of(context, StatusCodes.Status422UnprocessableEntity, ErrorCodes.ValidationFailed, "Check the highlighted fields", invalid);
        }

        var email = request!.Email!.Trim().ToLowerInvariant();
        var address = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (throttle.RetryAfter(email, address) is { } wait)
        {
            context.Response.Headers.RetryAfter = wait.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Problems.Of(context, StatusCodes.Status429TooManyRequests, ErrorCodes.TooManyAttempts, "Too many sign-in attempts");
        }

        var staff = await db.StaffAccounts.FirstOrDefaultAsync(account => account.Email.ToLower() == email, context.RequestAborted);

        // The hash is checked even for an unknown email, so timing does not reveal which accounts exist.
        var verified = hasher.VerifyHashedPassword(staff!, staff?.PasswordHash ?? DummyHash(hasher), request.Password!);
        if (staff is null || staff.Status != AccountStatus.Active || verified == PasswordVerificationResult.Failed)
        {
            throttle.RecordFailure(email, address);
            return Problems.Of(context, StatusCodes.Status401Unauthorized, ErrorCodes.InvalidCredentials, "Wrong email or password");
        }

        throttle.RecordSuccess(email);
        staff.LastSignInAt = clock.GetUtcNow();
        var issued = refresh.IssueForSignIn(staff);
        db.AuditEvents.Add(AuditLog.Event(context, ActorType.User, staff.Id, null, "SIGNED_IN", "staff_account", staff.Id.ToString()));
        await db.SaveChangesAsync(context.RequestAborted);

        SetCookie(context, issued, clock);
        return Results.Ok(Session(staff, tokens));
    }

    private static async Task<IResult> Refresh(
        HttpContext context,
        RefreshTokenStore refresh,
        TokenService tokens,
        TimeProvider clock,
        IOptions<AuthOptions> options)
    {
        if (!OriginAllowed(context, options.Value))
        {
            return Problems.Of(context, StatusCodes.Status403Forbidden, ErrorCodes.OriginNotAllowed, "This origin is not allowed");
        }

        var presented = context.Request.Cookies[CookieName];
        var rotated = string.IsNullOrEmpty(presented) ? null : await refresh.RotateAsync(presented, context.RequestAborted);
        if (rotated is not { } result)
        {
            return Problems.Of(context, StatusCodes.Status401Unauthorized, ErrorCodes.RefreshTokenInvalid, "Sign in again");
        }

        SetCookie(context, result.Token, clock);
        return Results.Ok(Session(result.Staff, tokens));
    }

    private static async Task<IResult> Logout(
        HttpContext context,
        ClaimLensDbContext db,
        RefreshTokenStore refresh,
        IOptions<AuthOptions> options)
    {
        if (!OriginAllowed(context, options.Value))
        {
            return Problems.Of(context, StatusCodes.Status403Forbidden, ErrorCodes.OriginNotAllowed, "This origin is not allowed");
        }

        var staffId = await refresh.RevokeAsync(context.Request.Cookies[CookieName], context.RequestAborted);
        if (staffId is { } id)
        {
            db.AuditEvents.Add(AuditLog.Event(context, ActorType.User, id, null, "SIGNED_OUT", "staff_account", id.ToString()));
            await db.SaveChangesAsync(context.RequestAborted);
        }

        context.Response.Cookies.Delete(CookieName, CookieOptionsFor(null));
        return Results.NoContent();
    }

    private static async Task<IResult> GetMe(HttpContext context, ClaimLensDbContext db)
    {
        context.User.TryGetStaff(out var staffId, out _);
        var staff = await db.StaffAccounts.AsNoTracking()
            .FirstOrDefaultAsync(account => account.Id == staffId, context.RequestAborted);

        return staff is null
            ? Problems.Of(context, StatusCodes.Status401Unauthorized, ErrorCodes.AuthRequired, "Sign in to continue")
            : Results.Ok(ToUser(staff));
    }

    private static List<FieldError> Validate(LoginRequest? request)
    {
        var errors = new List<FieldError>();
        if (string.IsNullOrWhiteSpace(request?.Email))
        {
            errors.Add(new FieldError("email", "REQUIRED", "Enter your email."));
        }

        if (string.IsNullOrEmpty(request?.Password))
        {
            errors.Add(new FieldError("password", "REQUIRED", "Enter your password."));
        }
        else if (request.Password.Length > 200)
        {
            errors.Add(new FieldError("password", "TOO_LONG", "This password is too long."));
        }

        return errors;
    }

    /// <summary>Compares against the configured web app. A request with no Origin is refused too.</summary>
    private static bool OriginAllowed(HttpContext context, AuthOptions options) =>
        options.AllowedOrigins.Contains(context.Request.Headers.Origin.ToString(), StringComparer.OrdinalIgnoreCase);

    private static void SetCookie(HttpContext context, RefreshTokenStore.Issued issued, TimeProvider clock)
    {
        var left = issued.ExpiresAt - clock.GetUtcNow();
        var maxAge = TimeSpan.FromSeconds(Math.Ceiling(left.TotalSeconds));
        context.Response.Cookies.Append(CookieName, issued.Token, CookieOptionsFor(maxAge));
    }

    private static CookieOptions CookieOptionsFor(TimeSpan? maxAge) =>
        new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
            MaxAge = maxAge,
        };

    private static AuthSession Session(StaffAccount staff, TokenService tokens) =>
        new(tokens.CreateAccessToken(staff), "Bearer", tokens.AccessTokenSeconds, ToUser(staff));

    private static StaffUser ToUser(StaffAccount staff) =>
        new(staff.Id, staff.Email, staff.FullName, staff.Initials, staff.Role);

    private static string DummyHash(IPasswordHasher<StaffAccount> hasher) =>
        dummy ??= hasher.HashPassword(null!, Guid.NewGuid().ToString());

    private static string? dummy;
}
