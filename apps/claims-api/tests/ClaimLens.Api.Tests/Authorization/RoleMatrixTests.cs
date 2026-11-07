using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Http;

namespace ClaimLens.Api.Tests.Authorization;

/// <summary>
/// Definition of Done: every role on every operation. The cases come from the same table the API enforces, which is
/// generated from x-claimlens in the contract.
/// </summary>
public class RoleMatrixTests(ApiFactory api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static readonly Dictionary<StaffRole, string> Accounts = new()
    {
        [StaffRole.IntakeOfficer] = Staff.Intake,
        [StaffRole.ClaimsReviewer] = Staff.Reviewer,
        [StaffRole.SeniorApprover] = Staff.Approver,
        [StaffRole.Administrator] = Staff.Admin,
    };

    public static TheoryData<string> ProtectedMustOperations =>
        new(OperationPolicies.All
            .Where(policy => !policy.IsPublic && policy.Release == ReleaseKind.Must)
            .Select(policy => policy.OperationId));

    public static TheoryData<string> ShouldOperations =>
        new(OperationPolicies.All
            .Where(policy => policy.Release == ReleaseKind.Should)
            .Select(policy => policy.OperationId));

    [Fact]
    public void The_table_covers_all_59_operations() =>
        Assert.Equal(59, OperationPolicies.All.Count);

    [Theory]
    [MemberData(nameof(ProtectedMustOperations))]
    public async Task Without_a_token_the_operation_answers_401(string operationId)
    {
        var response = await Call(api.Client(), OperationPolicies.Find(operationId)!);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("AUTH_REQUIRED", (await response.Content.ReadFromJsonAsync<ProblemBody>(Cancel))!.Code);
    }

    [Theory]
    [MemberData(nameof(ProtectedMustOperations))]
    public async Task Only_the_listed_roles_get_past_the_role_check(string operationId)
    {
        var policy = OperationPolicies.Find(operationId)!;

        foreach (var (role, email) in Accounts)
        {
            var client = await api.SignedInAs(email);
            var response = await Call(client, policy, role: role);

            if (policy.Roles.ContainsKey(role))
            {
                // Past the role check: 404 for a claim that does not exist, or the handler.
                Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    Assert.NotEqual("FORBIDDEN_ROLE", (await response.Content.ReadFromJsonAsync<ProblemBody>(Cancel))!.Code);
                }
            }
            else
            {
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                Assert.Equal("FORBIDDEN_ROLE", (await response.Content.ReadFromJsonAsync<ProblemBody>(Cancel))!.Code);
            }
        }
    }

    [Theory]
    [MemberData(nameof(ShouldOperations))]
    public async Task A_should_operation_is_hidden_until_the_release_switch_is_on(string operationId)
    {
        // The contract describes the full API. This 404 is the release switch, so it is not checked against the contract.
        var client = await api.SignedInAs(Staff.Approver);
        var hidden = api.CreateClient(new() { BaseAddress = new Uri("https://localhost/api/v1/") });
        hidden.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;

        var response = await Call(hidden, OperationPolicies.Find(operationId)!);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    internal static Task<HttpResponseMessage> Call(HttpClient client, OperationPolicy policy, Guid? claimId = null, StaffRole? role = null)
    {
        var path = Regex.Replace(policy.Path, @"\{(\w+)\}", match =>
            match.Groups[1].Value == "claimNumber"
                ? "CLM-2026-999999"
                : (match.Groups[1].Value == "claimId" && claimId is not null ? claimId.Value : Guid.NewGuid()).ToString());

        // listClaims needs a view, and a view of another role is FORBIDDEN_ROLE too, so each role asks for one of its own.
        var query = policy.OperationId == "listClaims" ? "?view=" + ViewOf(role) : "";
        using var request = new HttpRequestMessage(new HttpMethod(policy.Method), path.TrimStart('/') + query);
        return client.SendAsync(request, Cancel);
    }

    private static string ViewOf(StaffRole? role) =>
        role switch
        {
            StaffRole.IntakeOfficer => "MY_DRAFTS",
            StaffRole.SeniorApprover => "PENDING_APPROVAL",
            _ => "AVAILABLE",
        };
}
