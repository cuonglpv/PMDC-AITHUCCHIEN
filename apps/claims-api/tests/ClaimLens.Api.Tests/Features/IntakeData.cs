using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace ClaimLens.Api.Tests.Features;

public sealed record TestMember(Guid MemberId, Guid PolicyId, string FullName, string NationalId, string PolicyNumber);

/// <summary>Members, policies and claims for the intake tests. Each test makes its own, so tests do not share rows.</summary>
public static class IntakeData
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    /// <summary>The golden case's member, Nguyễn Văn An, whose balance is 5,000,000 − 4,200,000 = 800,000 VND.</summary>
    public const string AnMemberId = "30000000-0000-4000-8000-000000000001";
    public const string AnPolicyId = "31000000-0000-4000-8000-000000000001";
    public const string GoldPlan = "20000000-0000-4000-8000-000000000001";

    public static async Task<TestMember> NewMemberAsync(
        ApiFactory api,
        string? fullName = null,
        string status = "ACTIVE",
        string? terminatedOn = null,
        string coverageStart = "2026-01-01",
        string coverageEnd = "2026-12-31",
        long consumed = 0)
    {
        var memberId = Guid.CreateVersion7();
        var policyId = Guid.CreateVersion7();
        var tag = memberId.ToString("N")[^8..].ToUpperInvariant();
        var nationalId = string.Concat(Enumerable.Range(0, 12).Select(_ => (char)('0' + RandomNumberGenerator.GetInt32(10))));
        var name = fullName ?? $"Lê Thị Thử {tag}";
        var policyNumber = $"PT-{tag}";

        await api.Sql(
            $"""
            insert into member (id, member_code, full_name, national_id, date_of_birth)
            values ('{memberId}', 'MT-{tag}', '{name}', '{nationalId}', '1990-02-03');
            insert into policy (id, policy_number, member_id, plan_id, status, coverage_start, coverage_end, first_enrolled_on,
                                terminated_on, opening_consumed, consumed_amount)
            values ('{policyId}', '{policyNumber}', '{memberId}', '{GoldPlan}', '{status}', '{coverageStart}', '{coverageEnd}',
                    '{coverageStart}', {(terminatedOn is null ? "null" : $"'{terminatedOn}'")}, {consumed}, {consumed});
            """);

        return new TestMember(memberId, policyId, name, nationalId, policyNumber);
    }

    public static Task<HttpResponseMessage> CreateClaimAsync(
        HttpClient client,
        TestMember member,
        string serviceDate = "2026-10-12",
        long claimedTotal = 1_400_000,
        string providerName = "Phòng khám Đa khoa Hồng Ngọc",
        string channel = "COUNTER") =>
        client.PostAsJsonAsync(
            "claims",
            new { memberId = member.MemberId, policyId = member.PolicyId, serviceDate, providerName, claimedTotal, channel },
            Cancel);

    /// <summary>A new draft of a new member; its id.</summary>
    public static async Task<string> NewDraftAsync(ApiFactory api, HttpClient client)
    {
        var response = await CreateClaimAsync(client, await NewMemberAsync(api));
        response.EnsureSuccessStatusCode();
        return Id((await JsonOf(response)).GetProperty("claim"));
    }

    public static Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        string claimId,
        byte[] content,
        string fileName,
        string contentType = "application/octet-stream")
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return client.PostAsync($"claims/{claimId}/documents", form, Cancel);
    }

    /// <summary>Uploads a file and returns the new document's id.</summary>
    public static async Task<string> UploadedAsync(HttpClient client, string claimId, byte[] content, string fileName)
    {
        var response = await UploadAsync(client, claimId, content, fileName);
        response.EnsureSuccessStatusCode();
        return Id(await JsonOf(response));
    }

    public static async Task<List<JsonElement>> DocumentsAsync(HttpClient client, string claimId) =>
        (await JsonOf(await client.GetAsync($"claims/{claimId}/documents", Cancel))).GetProperty("items").EnumerateArray().ToList();

    /// <summary>Reads the body as text, so the same response can be read more than once.</summary>
    public static async Task<JsonElement> JsonOf(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(Cancel));

    public static async Task<string?> Code(HttpResponseMessage response) =>
        response.Content.Headers.ContentType?.MediaType == "application/problem+json"
            ? (await JsonOf(response)).GetProperty("code").GetString()
            : null;

    public static string Id(JsonElement item) => item.GetProperty("id").GetString()!;

    public static string RequestId(HttpResponseMessage response) => response.Headers.GetValues("X-Request-Id").Single();
}
