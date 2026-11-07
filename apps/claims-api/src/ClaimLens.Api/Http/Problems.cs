using System.Text.Json;

namespace ClaimLens.Api.Http;

/// <summary>Stable codes from the contract's ErrorCode list. Only the web app's messages depend on them.</summary>
public static class ErrorCodes
{
    public const string AuthRequired = "AUTH_REQUIRED";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string RefreshTokenInvalid = "REFRESH_TOKEN_INVALID";
    public const string OriginNotAllowed = "ORIGIN_NOT_ALLOWED";
    public const string TooManyAttempts = "TOO_MANY_ATTEMPTS";
    public const string ForbiddenRole = "FORBIDDEN_ROLE";
    public const string ClaimOutOfScope = "CLAIM_OUT_OF_SCOPE";
    public const string MakerChecker = "MAKER_CHECKER";
    public const string NotFound = "NOT_FOUND";
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string ClaimStateConflict = "CLAIM_STATE_CONFLICT";
    public const string DraftAlreadyExists = "DRAFT_ALREADY_EXISTS";
    public const string RuleSetNotDraft = "RULE_SET_NOT_DRAFT";
    public const string RuleInvalid = "RULE_INVALID";
    public const string UnknownCategory = "UNKNOWN_CATEGORY";
    public const string EffectiveDateNotLater = "EFFECTIVE_DATE_NOT_LATER";
    public const string CategoryNotConfigured = "CATEGORY_NOT_CONFIGURED";
    public const string InternalError = "INTERNAL_ERROR";
}

public sealed record FieldError(string Field, string Code, string Message);

/// <summary>RFC 9457 responses with the contract's code and requestId.</summary>
public static class Problems
{
    public const string ContentType = "application/problem+json";

    public static IResult Of(
        HttpContext context,
        int status,
        string code,
        string title,
        IReadOnlyList<FieldError>? errors = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["type"] = "about:blank",
            ["title"] = title,
            ["status"] = status,
            ["code"] = code,
            ["requestId"] = RequestIdMiddleware.Current(context),
        };

        if (errors is not null)
        {
            body["errors"] = errors;
        }

        return Results.Json(body, JsonOptions, ContentType, status);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
