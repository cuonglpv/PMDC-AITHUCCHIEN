using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace ClaimLens.Api.Tests.Http;

/// <summary>
/// X-Forwarded-For is believed only from a proxy named in Proxy:KnownProxies or Proxy:KnownNetworks. Anyone else could
/// otherwise write any address into audit_event.ip_address and slip past the per-address sign-in limit.
/// </summary>
public class ForwardedHeadersTests(ApiFactory api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private const string Caller = "203.0.113.9";
    private const string Forged = "198.51.100.77";

    [Fact]
    public async Task With_no_trusted_proxy_a_callers_header_is_ignored()
    {
        Assert.Equal(Caller, await SignInAddress(api, from: Caller, forwardedFor: Forged));
    }

    [Theory]
    [InlineData("Proxy:KnownProxies:0", Caller)]
    [InlineData("Proxy:KnownNetworks:0", "203.0.113.0/24")]
    public async Task A_trusted_proxy_passes_on_the_clients_address(string key, string value)
    {
        using var behindProxy = api.WithWebHostBuilder(builder => builder.UseSetting(key, value));

        Assert.Equal(Forged, await SignInAddress(behindProxy, from: Caller, forwardedFor: Forged));
    }

    [Fact]
    public async Task A_caller_that_is_not_the_trusted_proxy_is_still_ignored()
    {
        using var behindProxy = api.WithWebHostBuilder(builder => builder.UseSetting("Proxy:KnownProxies:0", "192.0.2.1"));

        Assert.Equal(Caller, await SignInAddress(behindProxy, from: Caller, forwardedFor: Forged));
    }

    /// <summary>Signs in from a given connection address and returns the address the audit event recorded.</summary>
    private async Task<string?> SignInAddress(WebApplicationFactory<Program> factory, string from, string forwardedFor)
    {
        var body = $$"""{"email":"{{Staff.Approver}}","password":"{{ApiFactory.Password}}"}""";
        var context = await factory.Server.SendAsync(
            request =>
            {
                request.Connection.RemoteIpAddress = IPAddress.Parse(from);
                request.Request.Method = HttpMethods.Post;
                request.Request.Scheme = "https";
                request.Request.Path = "/api/v1/auth/login";
                request.Request.ContentType = "application/json";
                request.Request.Headers["X-Forwarded-For"] = forwardedFor;
                request.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            },
            Cancel);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        var requestId = context.Response.Headers["X-Request-Id"].ToString();
        return await api.Scalar<string>($"select host(ip_address) from audit_event where request_id = '{requestId}'");
    }
}
