namespace ClaimLens.Api.AccessControl;

/// <summary>Identity data that leaves the API only in part (BR-11, docs/07-api-contract.md).</summary>
public static class Masking
{
    private const int Visible = 4;

    /// <summary>Only the last four digits are readable: `000000004821` becomes `********4821`.</summary>
    public static string NationalId(string nationalId) =>
        nationalId.Length <= Visible
            ? new string('*', Visible)
            : new string('*', nationalId.Length - Visible) + nationalId[^Visible..];
}
