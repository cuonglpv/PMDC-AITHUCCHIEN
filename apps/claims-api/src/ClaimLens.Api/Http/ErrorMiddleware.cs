using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Npgsql;

namespace ClaimLens.Api.Http;

/// <summary>
/// The API checks rules first and the database repeats the critical ones. A named constraint failure becomes the
/// same error code. Anything else is a bug and gives 500 INTERNAL_ERROR (docs/07-api-contract.md).
/// </summary>
public sealed class ErrorMiddleware(RequestDelegate next, ILogger<ErrorMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception error) when (!context.Response.HasStarted && Constraint(error) is { } constraint
            && ConstraintErrorMap.Find(constraint) is { } mapped)
        {
            logger.LogInformation("Constraint {Constraint} refused a change", constraint);
            await AnswerAsync(context, mapped.Status, mapped.Code, mapped.Title);
        }
        catch (BadHttpRequestException error) when (!context.Response.HasStarted)
        {
            logger.LogInformation(error, "Unreadable request");
            await AnswerAsync(context, StatusCodes.Status422UnprocessableEntity, ErrorCodes.ValidationFailed, "The request could not be read");
        }
        catch (Exception error) when (!context.Response.HasStarted && error is not OperationCanceledException)
        {
            // The body says nothing about the fault; requestId finds it in this log entry.
            logger.LogError(error, "Unhandled error in request {RequestId}", RequestIdMiddleware.Current(context));
            await AnswerAsync(context, StatusCodes.Status500InternalServerError, ErrorCodes.InternalError, "Something went wrong");
        }
    }

    /// <summary>
    /// Drops what the failed handler had already set, such as the Content-Length of a body that was held back, a cookie
    /// or a content type, and keeps X-Request-Id.
    /// </summary>
    private static async Task AnswerAsync(HttpContext context, int status, string code, string title)
    {
        var requestId = context.Response.Headers[RequestIdMiddleware.Header];
        context.Response.Clear();
        if (!StringValues.IsNullOrEmpty(requestId))
        {
            context.Response.Headers[RequestIdMiddleware.Header] = requestId;
        }

        await Problems.Of(context, status, code, title).ExecuteAsync(context);
    }

    private static string? Constraint(Exception error) =>
        error switch
        {
            DbUpdateException { InnerException: PostgresException inner } => inner.ConstraintName,
            PostgresException direct => direct.ConstraintName,
            _ => null,
        };
}
