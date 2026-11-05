using System.Security.Claims;
using System.Text.Json;
using ClaimLens.Api.Domain.Enums;

namespace ClaimLens.Api.AccessControl;

/// <summary>Reads the signed-in staff member from the access token's claims.</summary>
public static class StaffPrincipal
{
    public const string SubjectClaim = "sub";
    public const string RoleClaim = "role";

    public static string ToCode(StaffRole role) => JsonNamingPolicy.SnakeCaseUpper.ConvertName(role.ToString());

    public static bool TryGetStaff(this ClaimsPrincipal user, out Guid staffId, out StaffRole role)
    {
        staffId = Guid.Empty;
        role = default;

        if (user.Identity?.IsAuthenticated != true
            || !Guid.TryParse(user.FindFirstValue(SubjectClaim), out staffId))
        {
            return false;
        }

        var code = user.FindFirstValue(RoleClaim);
        foreach (var candidate in Enum.GetValues<StaffRole>())
        {
            if (ToCode(candidate) == code)
            {
                role = candidate;
                return true;
            }
        }

        return false;
    }
}
