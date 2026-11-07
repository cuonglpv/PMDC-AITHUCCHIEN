using System.Text.Json;
using ClaimLens.Api.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClaimLens.Api.Tests.Http;

/// <summary>Every error is application/problem+json with a code and requestId, a bug included (docs/07-api-contract.md).</summary>
public class ErrorMiddlewareTests(ApiFactory api)
{
    [Fact]
    public async Task An_unhandled_error_gives_500_INTERNAL_ERROR_as_a_problem()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var body = new MemoryStream();
        context.Response.Body = body;
        var requestIds = new RequestIdMiddleware(inner => new ErrorMiddleware(
            failing =>
            {
                // What a handler may have set before it failed must not reach the caller.
                failing.Response.ContentType = "image/webp";
                failing.Response.ContentLength = 52_000;
                failing.Response.Headers.CacheControl = "private, max-age=60";
                throw new InvalidOperationException("Something nobody expected");
            },
            NullLogger<ErrorMiddleware>.Instance).InvokeAsync(inner));

        await requestIds.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal(Problems.ContentType, context.Response.ContentType);
        Assert.Null(context.Response.ContentLength);
        Assert.False(context.Response.Headers.ContainsKey("Cache-Control"));

        var problem = JsonSerializer.Deserialize<ProblemBody>(body.ToArray(), JsonSerializerOptions.Web)!;
        Assert.Equal("INTERNAL_ERROR", problem.Code);
        Assert.Equal(500, problem.Status);
        Assert.Equal(context.Response.Headers[RequestIdMiddleware.Header].ToString(), problem.RequestId);
        Assert.DoesNotContain("nobody expected", problem.Title, StringComparison.Ordinal);
    }
}
