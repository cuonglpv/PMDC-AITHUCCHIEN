using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using ClaimLens.Api.AccessControl;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ClaimLens.Api.Tests.Features;

/// <summary>Auth settings are checked when the API starts, and access tokens name who issued them and for whom.</summary>
public class AuthOptionsTests(ApiFactory api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private const string SigningKey = "test-only-signing-key-long-enough-for-hs256";

    [Theory]
    [InlineData("Auth:SigningKey", "only-31-bytes-is-not-enough-xx")]
    [InlineData("Auth:SigningKey", "")]
    [InlineData("Auth:AllowedOrigins:0", "")]
    [InlineData("Auth:Issuer", "")]
    [InlineData("Auth:Audience", "")]
    public void The_api_does_not_start_with_unsafe_auth_settings(string key, string value)
    {
        using var factory = api.WithWebHostBuilder(builder => builder.UseSetting(key, value));

        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Equal(typeof(AuthOptions), error.OptionsType);
    }

    [Fact]
    public void No_allowed_origin_at_all_is_refused()
    {
        var result = new AuthOptionsValidator().Validate(null, new AuthOptions { SigningKey = SigningKey });

        Assert.True(result.Failed);
        Assert.Contains("Auth:AllowedOrigins", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Safe_settings_pass()
    {
        var result = new AuthOptionsValidator().Validate(null, new AuthOptions { SigningKey = SigningKey, AllowedOrigins = [ApiFactory.Origin] });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Issued_tokens_name_the_issuer_and_audience()
    {
        var client = await api.SignedInAs(Staff.Reviewer);
        var token = new JsonWebToken(client.DefaultRequestHeaders.Authorization!.Parameter);

        Assert.Equal(new AuthOptions().Issuer, token.Issuer);
        Assert.Equal([new AuthOptions().Audience], token.Audiences);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("another-service", null)]
    [InlineData(null, "another-app")]
    public async Task A_token_for_another_issuer_or_audience_gets_AUTH_REQUIRED(string? issuer, string? audience)
    {
        var options = new AuthOptions();
        var now = api.Clock.GetUtcNow().UtcDateTime;
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(StaffPrincipal.SubjectClaim, Staff.ReviewerId),
                new Claim(StaffPrincipal.RoleClaim, "CLAIMS_REVIEWER"),
            ]),
            // The same key, so only the issuer or audience is wrong. Null leaves the claim out.
            Issuer = issuer ?? (audience is null ? null : options.Issuer),
            Audience = audience ?? (issuer is null ? null : options.Audience),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256),
        });
        var client = api.Client();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("me", Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
