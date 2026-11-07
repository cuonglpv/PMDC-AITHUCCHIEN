using System.Text;
using Microsoft.Extensions.Options;

namespace ClaimLens.Api.AccessControl;

public sealed class AuthOptions
{
    public string SigningKey { get; set; } = "";
    public string Issuer { get; set; } = "claimlens-claims-api";
    public string Audience { get; set; } = "claimlens-staff-web";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenHours { get; set; } = 8;
    public int RotationGraceSeconds { get; set; } = 10;
    public string[] AllowedOrigins { get; set; } = [];
    public int SignInFailuresPerEmail { get; set; } = 5;
    public int SignInFailuresPerAddress { get; set; } = 20;
    public int SignInWindowMinutes { get; set; } = 15;
}

/// <summary>Runs when the API starts, so a weak key or a missing origin stops it instead of the first sign-in.</summary>
public sealed class AuthOptionsValidator : IValidateOptions<AuthOptions>
{
    /// <summary>HS256 needs a 256-bit key.</summary>
    public const int MinimumKeyBytes = 32;

    public ValidateOptionsResult Validate(string? name, AuthOptions options)
    {
        var failures = new List<string>();

        if (Encoding.UTF8.GetByteCount(options.SigningKey ?? "") < MinimumKeyBytes)
        {
            failures.Add($"Auth:SigningKey must be at least {MinimumKeyBytes} bytes.");
        }

        if (options.AllowedOrigins is not { Length: > 0 } || options.AllowedOrigins.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add("Auth:AllowedOrigins must list the Staff Web App's origin; refresh and logout accept no other.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("Auth:Issuer must be set.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Auth:Audience must be set.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
