using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ClaimLens.Api.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ClaimLens.Api.AccessControl;

/// <summary>A signed access token that lives 15 minutes, and opaque refresh tokens stored only as a hash.</summary>
public sealed class TokenService(IOptions<AuthOptions> options, TimeProvider clock)
{
    private readonly JsonWebTokenHandler handler = new();

    public int AccessTokenSeconds => options.Value.AccessTokenMinutes * 60;

    public string CreateAccessToken(StaffAccount staff)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.SigningKey));

        return handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new System.Security.Claims.Claim(StaffPrincipal.SubjectClaim, staff.Id.ToString()),
                new System.Security.Claims.Claim(StaffPrincipal.RoleClaim, StaffPrincipal.ToCode(staff.Role)),
            ]),
            Issuer = options.Value.Issuer,
            Audience = options.Value.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddSeconds(AccessTokenSeconds),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        });
    }

    public static string NewRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    public static string HashRefreshToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
