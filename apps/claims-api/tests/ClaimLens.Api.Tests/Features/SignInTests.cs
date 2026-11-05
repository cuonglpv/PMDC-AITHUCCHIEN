using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClaimLens.Api.AccessControl;

namespace ClaimLens.Api.Tests.Features;

/// <summary>US01 and the token rules in docs/07-api-contract.md. The contract handler checks every response too.</summary>
public class SignInTests(ApiFactory api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(Staff.Intake, "INTAKE_OFFICER")]
    [InlineData(Staff.Reviewer, "CLAIMS_REVIEWER")]
    [InlineData(Staff.Approver, "SENIOR_APPROVER")]
    [InlineData(Staff.Admin, "ADMINISTRATOR")]
    public async Task Valid_credentials_return_a_token_and_the_role(string email, string role)
    {
        var response = await api.Client().PostAsJsonAsync("auth/login", new { email, password = ApiFactory.Password }, Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SessionWithUser>(Cancel);
        Assert.Equal("Bearer", body!.TokenType);
        Assert.Equal(900, body.ExpiresIn);
        Assert.Equal(role, body.User.Role);
        Assert.False(string.IsNullOrEmpty(body.AccessToken));
    }

    [Fact]
    public async Task The_refresh_cookie_has_the_agreed_attributes()
    {
        var response = await Login(Staff.Reviewer);

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith($"{AuthEndpoints.CookieName}=", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=28800", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_refresh_token_itself_is_never_stored()
    {
        var token = CookieValue(await Login(Staff.Reviewer));

        var stored = await api.Scalar<long>($"select count(*) from refresh_token where token_hash = '{token}'");

        Assert.Equal(0, stored);
    }

    [Theory]
    [InlineData("wrong.password@claimlens.test", "disabled")]
    [InlineData("nobody@claimlens.test", "unknown")]
    public async Task Unknown_email_gets_the_same_answer_as_a_wrong_password(string email, string _)
    {
        var unknown = await api.Client().PostAsJsonAsync("auth/login", new { email, password = "x" }, Cancel);
        var wrong = await api.Client().PostAsJsonAsync("auth/login", new { email = Staff.Admin, password = "wrong" }, Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        var first = await unknown.Content.ReadFromJsonAsync<ProblemBody>(Cancel);
        var second = await wrong.Content.ReadFromJsonAsync<ProblemBody>(Cancel);
        Assert.Equal("INVALID_CREDENTIALS", first!.Code);
        Assert.Equal(first.Code, second!.Code);
        Assert.Equal(first.Title, second.Title);
    }

    [Fact]
    public async Task A_disabled_account_gets_the_same_answer_too()
    {
        await api.Sql(
            $"""
            insert into staff_account (email, password_hash, full_name, initials, role, status)
            select 'disabled@claimlens.test', password_hash, 'Disabled', 'DD', 'CLAIMS_REVIEWER', 'DISABLED'
            from staff_account where id = '{Staff.IntakeId}'
            on conflict do nothing
            """);

        var response = await api.Client().PostAsJsonAsync("auth/login", new { email = "disabled@claimlens.test", password = ApiFactory.Password }, Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("INVALID_CREDENTIALS", (await response.Content.ReadFromJsonAsync<ProblemBody>(Cancel))!.Code);
    }

    [Fact]
    public async Task Missing_values_list_each_field()
    {
        var response = await api.Client().PostAsJsonAsync("auth/login", new { email = "" }, Cancel);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Cancel);
        Assert.Contains("\"field\":\"email\"", body, StringComparison.Ordinal);
        Assert.Contains("\"field\":\"password\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Too_many_failures_for_one_email_are_refused_and_say_when_to_retry()
    {
        const string Email = "throttled@claimlens.test";
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await api.Client().PostAsJsonAsync("auth/login", new { email = Email, password = "x" }, Cancel);
        }

        var response = await api.Client().PostAsJsonAsync("auth/login", new { email = Email, password = "x" }, Cancel);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.True(response.Headers.RetryAfter?.Delta > TimeSpan.Zero);
    }

    [Fact]
    public async Task Sign_in_writes_an_audit_event()
    {
        var response = await Login(Staff.Approver);

        var requestId = response.Headers.GetValues("X-Request-Id").Single();
        var events = await api.Scalar<long>($"select count(*) from audit_event where event_type = 'SIGNED_IN' and request_id = '{requestId}'");
        Assert.Equal(1, events);
    }

    [Fact]
    public async Task GetMe_returns_the_signed_in_user()
    {
        var client = await api.SignedInAs(Staff.Reviewer);

        var user = await client.GetFromJsonAsync<UserBody>("me", Cancel);

        Assert.Equal(Staff.Reviewer, user!.Email);
        Assert.Equal("TH", user.Initials);
    }

    [Fact]
    public async Task No_token_gets_AUTH_REQUIRED()
    {
        var response = await api.Client().GetAsync("me", Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("AUTH_REQUIRED", (await response.Content.ReadFromJsonAsync<ProblemBody>(Cancel))!.Code);
    }

    [Fact]
    public async Task A_forged_token_gets_AUTH_REQUIRED()
    {
        var client = api.Client();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.token");

        var response = await client.GetAsync("me", Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_expired_access_token_gets_AUTH_REQUIRED_so_the_app_refreshes()
    {
        var client = await api.SignedInAs(Staff.Reviewer);
        api.Clock.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));

        var response = await client.GetAsync("me", Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("AUTH_REQUIRED", (await response.Content.ReadFromJsonAsync<ProblemBody>(Cancel))!.Code);
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_returns_a_new_access_token()
    {
        var first = CookieValue(await Login(Staff.Reviewer));

        var response = await Refresh(first);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(first, CookieValue(response));
    }

    [Fact]
    public async Task Refresh_never_extends_the_eight_hour_limit()
    {
        var first = CookieValue(await Login(Staff.Reviewer));
        api.Clock.Advance(TimeSpan.FromHours(1));

        var response = await Refresh(first);

        var cookie = response.Headers.GetValues("Set-Cookie").Single();
        Assert.Contains("max-age=25200", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_rotated_token_works_for_ten_seconds_so_two_tabs_can_refresh()
    {
        var first = CookieValue(await Login(Staff.Reviewer));
        await Refresh(first);
        api.Clock.Advance(TimeSpan.FromSeconds(9));

        var second = await Refresh(first);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task Reuse_after_the_grace_period_ends_the_whole_sign_in()
    {
        var first = CookieValue(await Login(Staff.Reviewer));
        var rotated = CookieValue(await Refresh(first));
        api.Clock.Advance(TimeSpan.FromSeconds(11));

        var reused = await Refresh(first);
        var afterwards = await Refresh(rotated);

        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
        Assert.Equal("REFRESH_TOKEN_INVALID", (await reused.Content.ReadFromJsonAsync<ProblemBody>(Cancel))!.Code);
        Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode);
    }

    [Fact]
    public async Task A_refresh_token_stops_working_after_eight_hours()
    {
        var first = CookieValue(await Login(Staff.Reviewer));
        api.Clock.Advance(TimeSpan.FromHours(8) + TimeSpan.FromSeconds(1));

        var response = await Refresh(first);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_from_another_origin_is_refused()
    {
        var token = CookieValue(await Login(Staff.Reviewer));

        var response = await Refresh(token, "https://evil.test");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("ORIGIN_NOT_ALLOWED", (await response.Content.ReadFromJsonAsync<ProblemBody>(Cancel))!.Code);
    }

    [Fact]
    public async Task Refresh_with_no_origin_is_refused()
    {
        var token = CookieValue(await Login(Staff.Reviewer));

        var response = await Refresh(token, origin: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_sign_in_and_clears_the_cookie()
    {
        var token = CookieValue(await Login(Staff.Reviewer));

        var logout = await Post("auth/logout", token);
        var afterwards = await Refresh(token);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains("expires=", logout.Headers.GetValues("Set-Cookie").Single(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode);
    }

    [Fact]
    public async Task Logout_is_safe_to_repeat()
    {
        var token = CookieValue(await Login(Staff.Reviewer));

        await Post("auth/logout", token);
        var again = await Post("auth/logout", token);

        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
    }

    [Fact]
    public async Task Refresh_stops_for_a_disabled_account()
    {
        await api.Sql(
            $"""
            insert into staff_account (email, password_hash, full_name, initials, role)
            select 'to-disable@claimlens.test', password_hash, 'To Disable', 'TD', 'CLAIMS_REVIEWER'
            from staff_account where id = '{Staff.IntakeId}'
            on conflict do nothing
            """);
        var token = CookieValue(await Login("to-disable@claimlens.test"));
        await api.Sql("update staff_account set status = 'DISABLED' where email = 'to-disable@claimlens.test'");

        var response = await Refresh(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<HttpResponseMessage> Login(string email)
    {
        var response = await api.Client().PostAsJsonAsync("auth/login", new { email, password = ApiFactory.Password }, Cancel);
        response.EnsureSuccessStatusCode();
        return response;
    }

    private Task<HttpResponseMessage> Refresh(string token, string? origin = ApiFactory.Origin) => Post("auth/refresh", token, origin);

    private async Task<HttpResponseMessage> Post(string path, string token, string? origin = ApiFactory.Origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("Cookie", $"{AuthEndpoints.CookieName}={token}");
        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        return await api.Client().SendAsync(request, Cancel);
    }

    private static string CookieValue(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single().Split(';')[0].Split('=', 2)[1];

    private sealed record SessionWithUser(string AccessToken, string TokenType, int ExpiresIn, UserBody User);

    private sealed record UserBody(string Id, string Email, string FullName, string Initials, string Role);
}
