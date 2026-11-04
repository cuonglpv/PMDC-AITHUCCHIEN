using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClaimLens.Api.Http;

/// <summary>
/// The API checks rules first and the database repeats the critical ones. A named constraint failure becomes the
/// same error code. Anything else is a bug and gives 500 (docs/07-api-contract.md).
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
            await Problems.Of(context, mapped.Status, mapped.Code, mapped.Title).ExecuteAsync(context);
        }
        catch (BadHttpRequestException error) when (!context.Response.HasStarted)
        {
            logger.LogInformation(error, "Unreadable request");
            await Problems.Of(
                    context,
                    StatusCodes.Status422UnprocessableEntity,
                    ErrorCodes.ValidationFailed,
                    "The request could not be read")
                .ExecuteAsync(context);
        }
        catch (Exception error) when (!context.Response.HasStarted && error is not OperationCanceledException)
        {
            logger.LogError(error, "Unhandled error");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        }
    }

    private static string? Constraint(Exception error) =>
        error switch
        {
            DbUpdateException { InnerException: PostgresException inner } => inner.ConstraintName,
            PostgresException direct => direct.ConstraintName,
            _ => null,
        };
}
