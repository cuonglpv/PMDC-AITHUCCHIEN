namespace ClaimLens.Api.AccessControl;

public sealed class AuthOptions
{
    public string SigningKey { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenHours { get; set; } = 8;
    public int RotationGraceSeconds { get; set; } = 10;
    public string[] AllowedOrigins { get; set; } = [];
    public int SignInFailuresPerEmail { get; set; } = 5;
    public int SignInFailuresPerAddress { get; set; } = 20;
    public int SignInWindowMinutes { get; set; } = 15;
}
