using System.Text.Json;

namespace ClaimLens.Api.Http;

public static class Requests
{
    /// <summary>Null for a body that cannot be read, so the handler answers 422 rather than the framework's 400.</summary>
    public static async Task<T?> ReadJsonAsync<T>(HttpContext context)
        where T : class
    {
        try
        {
            return await context.Request.ReadFromJsonAsync<T>(context.RequestAborted);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or BadHttpRequestException)
        {
            return null;
        }
    }

    public static IResult Unreadable(HttpContext context, params FieldError[] errors) =>
        Problems.Of(
            context,
            StatusCodes.Status422UnprocessableEntity,
            ErrorCodes.ValidationFailed,
            "The request could not be read",
            errors);

    public static IResult NotFound(HttpContext context) =>
        Problems.Of(context, StatusCodes.Status404NotFound, ErrorCodes.NotFound, "Not found");
}
