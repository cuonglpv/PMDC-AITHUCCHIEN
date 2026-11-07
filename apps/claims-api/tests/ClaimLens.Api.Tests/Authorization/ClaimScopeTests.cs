using System.Net;
using System.Net.Http.Json;
using System.Text;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ClaimLens.Api.Tests.Authorization;

/// <summary>BR-11 and the claim statuses in x-claimlens, on real rows moved through the lifecycle trigger.</summary>
public class ClaimScopeTests(ApiFactory api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private const string Member = "30000000-0000-4000-8000-000000000001";
    private const string Policy = "31000000-0000-4000-8000-000000000001";

    public static TheoryData<string> ClaimOperations =>
        new(OperationPolicies.All
            .Where(policy => policy.Path.Contains("{claimId}", StringComparison.Ordinal) && policy.Release == ReleaseKind.Must)
            .Select(policy => policy.OperationId));

    [Theory]
    [MemberData(nameof(ClaimOperations))]
    public async Task Each_role_reaches_only_the_claims_in_its_scope(string operationId)
    {
        var policy = OperationPolicies.Find(operationId)!;
        var claim = await NewClaimInReview();

        if (policy.Roles.TryGetValue(StaffRole.ClaimsReviewer, out var reviewerScope))
        {
            var owner = await Call(Staff.Reviewer, policy, claim);
            Assert.NotEqual("CLAIM_OUT_OF_SCOPE", await Code(owner));

            var other = await Call(Staff.OtherReviewer, policy, claim);
            if (reviewerScope == ClaimScopeKind.Assigned)
            {
                Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
                Assert.Equal("CLAIM_OUT_OF_SCOPE", await Code(other));
            }
        }

        if (policy.Roles.ContainsKey(StaffRole.SeniorApprover))
        {
            Assert.NotEqual("CLAIM_OUT_OF_SCOPE", await Code(await Call(Staff.Approver, policy, claim)));
        }

        if (policy.Roles.ContainsKey(StaffRole.IntakeOfficer))
        {
            Assert.NotEqual("CLAIM_OUT_OF_SCOPE", await Code(await Call(Staff.Intake, policy, claim)));

            var someoneElses = await NewClaimInReview(registeredByOtherIntake: true);
            Assert.Equal("CLAIM_OUT_OF_SCOPE", await Code(await Call(Staff.Intake, policy, someoneElses)));
        }
    }

    [Theory]
    [MemberData(nameof(ClaimOperations))]
    public async Task A_status_outside_the_list_gives_409(string operationId)
    {
        var policy = OperationPolicies.Find(operationId)!;
        if (policy.States is not { } states)
        {
            return;
        }

        var claim = states.Contains(ClaimStatus.PendingApproval) ? await NewClaimInReview() : await NewClaimPendingApproval();
        var status = states.Contains(ClaimStatus.PendingApproval) ? ClaimStatus.InReview : ClaimStatus.PendingApproval;
        if (states.Contains(status))
        {
            return;
        }

        var email = policy.Roles.ContainsKey(StaffRole.ClaimsReviewer)
            ? Staff.Reviewer
            : policy.Roles.ContainsKey(StaffRole.SeniorApprover) ? Staff.Approver : Staff.Intake;

        var response = await Call(email, policy, claim);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("CLAIM_STATE_CONFLICT", await Code(response));
    }

    [Fact]
    public async Task A_claim_that_does_not_exist_gives_404()
    {
        var client = await api.SignedInAs(Staff.Approver);

        var response = await RoleMatrixTests.Call(client, OperationPolicies.Find("getClaim")!);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_read_writes_one_audit_event_after_it_succeeds()
    {
        var claim = await NewClaimInReview();
        var policy = OperationPolicies.Find("getClaim")!;

        var response = await InvokeMiddleware(policy, claim, handlerStatus: StatusCodes.Status200OK);

        Assert.Equal(StatusCodes.Status200OK, response.Status);
        Assert.Equal(ClaimData, response.Body);
        Assert.Equal(1, await api.Scalar<long>($"select count(*) from audit_event where event_type = 'CLAIM_VIEWED' and claim_id = '{claim}'"));
    }

    [Fact]
    public async Task A_failed_read_writes_no_audit_event()
    {
        var claim = await NewClaimInReview();
        var policy = OperationPolicies.Find("getClaim")!;

        await InvokeMiddleware(policy, claim, handlerStatus: StatusCodes.Status500InternalServerError);

        Assert.Equal(0, await api.Scalar<long>($"select count(*) from audit_event where claim_id = '{claim}'"));
    }

    [Fact]
    public async Task A_read_whose_audit_event_cannot_be_saved_sends_an_error_and_no_claim_data()
    {
        var claim = await NewClaimInReview();
        var policy = OperationPolicies.Find("getClaim")!;

        var response = await InvokeMiddleware(policy, claim, handlerStatus: StatusCodes.Status200OK, auditFails: true);

        Assert.DoesNotContain("Hồng Ngọc", response.Body, StringComparison.Ordinal);
        Assert.Equal(StatusCodes.Status500InternalServerError, response.Status);
        Assert.Equal(Problems.ContentType, response.ContentType);
        Assert.Contains("\"code\":\"INTERNAL_ERROR\"", response.Body, StringComparison.Ordinal);
        Assert.True(response.ContentLength is null || response.ContentLength == Encoding.UTF8.GetByteCount(response.Body));
        Assert.Equal(0, await api.Scalar<long>($"select count(*) from audit_event where claim_id = '{claim}'"));
    }

    [Fact]
    public async Task Polling_status_is_the_one_claim_read_that_writes_no_event()
    {
        var claim = await NewClaimInReview();
        var policy = OperationPolicies.Find("getClaimStatus")!;

        await InvokeMiddleware(policy, claim, handlerStatus: StatusCodes.Status200OK);

        Assert.Equal(0, await api.Scalar<long>($"select count(*) from audit_event where claim_id = '{claim}'"));
    }

    private const string ClaimData = """{"providerName":"Hồng Ngọc","claimedTotal":1400000}""";

    private sealed record MiddlewareResponse(int Status, string? ContentType, long? ContentLength, string Body);

    /// <summary>
    /// Runs the real ErrorMiddleware and OperationPolicyMiddleware around a stand-in handler that writes claim data, as
    /// getClaim does, so a test can make the handler or the audit insert fail at will.
    /// </summary>
    private async Task<MiddlewareResponse> InvokeMiddleware(OperationPolicy policy, Guid claim, int handlerStatus, bool auditFails = false)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var server = new ServerResponse();
        context.Features.Set<IHttpResponseFeature>(server);
        context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(server.Sent));
        context.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [
                new System.Security.Claims.Claim("sub", Staff.ReviewerId),
                new System.Security.Claims.Claim("role", "CLAIMS_REVIEWER"),
            ],
            "test"));
        context.Request.RouteValues["claimId"] = claim.ToString();
        context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new EndpointNameMetadata(policy.OperationId)), policy.OperationId));

        await using var db = auditFails
            ? new ClaimLensDbContext(new DbContextOptionsBuilder<ClaimLensDbContext>()
                .UseNpgsql(api.AppConnection, EnumMapping.MapAll)
                .AddInterceptors(new RefuseSaves())
                .Options)
            : scope.ServiceProvider.GetRequiredService<ClaimLensDbContext>();

        var bytes = Encoding.UTF8.GetBytes(ClaimData);
        var policyMiddleware = new OperationPolicyMiddleware(async inner =>
        {
            inner.Response.StatusCode = handlerStatus;
            if (handlerStatus == StatusCodes.Status200OK)
            {
                inner.Response.ContentType = "application/json";
                inner.Response.ContentLength = bytes.Length;
                await inner.Response.Body.WriteAsync(bytes);
            }
        });
        var release = scope.ServiceProvider.GetRequiredService<IOptions<ReleaseOptions>>();
        var errors = new ErrorMiddleware(inner => policyMiddleware.InvokeAsync(inner, db, release), NullLogger<ErrorMiddleware>.Instance);

        try
        {
            await errors.InvokeAsync(context);
        }
        catch (Exception) when (server.HasStarted)
        {
            // A real server would now drop the connection, but whatever was sent has reached the caller.
        }

        return new MiddlewareResponse(
            context.Response.StatusCode,
            context.Response.ContentType,
            context.Response.ContentLength,
            Encoding.UTF8.GetString(server.Sent.ToArray()));
    }

    /// <summary>Like Kestrel: once a byte has gone out, the status and headers are sent and can no longer change.</summary>
    private sealed class ServerResponse : HttpResponseFeature
    {
        public MemoryStream Sent { get; } = new();

        public override bool HasStarted => Sent.Length > 0;
    }

    private sealed class RefuseSaves : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("The audit insert failed.");
    }

    private async Task<HttpResponseMessage> Call(string email, OperationPolicy policy, Guid claim) =>
        await RoleMatrixTests.Call(await api.SignedInAs(email), policy, claim);

    private static async Task<string?> Code(HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentType?.MediaType != Problems.ContentType)
        {
            return null;
        }

        return (await response.Content.ReadFromJsonAsync<ProblemBody>(Cancel))!.Code;
    }

    private async Task<Guid> NewClaimInReview(bool registeredByOtherIntake = false)
    {
        var id = Guid.CreateVersion7();
        var intake = Staff.IntakeId;
        if (registeredByOtherIntake)
        {
            intake = "10000000-0000-4000-8000-0000000000a1";
            await api.Sql(
                $"""
                insert into staff_account (id, email, password_hash, full_name, initials, role)
                values ('{intake}', 'intake2@claimlens.test', 'x', 'Other Intake', 'OI', 'INTAKE_OFFICER')
                on conflict do nothing
                """);
        }

        await api.Sql(
            $"""
            insert into claim (id, claim_number, member_id, policy_id, provider_name, service_date, claimed_total, channel, registered_by)
            values ('{id}', 'T-{id.ToString("N")[20..]}', '{Member}', '{Policy}', 'Hồng Ngọc', '2026-10-12', 1400000, 'COUNTER', '{intake}');
            update claim set status = 'PROCESSING' where id = '{id}';
            update claim set status = 'READY_FOR_REVIEW' where id = '{id}';
            update claim set status = 'IN_REVIEW', assignee_id = '{Staff.ReviewerId}', assigned_at = now() where id = '{id}';
            """);
        return id;
    }

    private async Task<Guid> NewClaimPendingApproval()
    {
        var id = await NewClaimInReview();
        await api.Sql($"update claim set status = 'PENDING_APPROVAL' where id = '{id}'");
        return id;
    }
}
