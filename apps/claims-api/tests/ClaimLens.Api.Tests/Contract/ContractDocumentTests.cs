namespace ClaimLens.Api.Tests.Contract;

/// <summary>The checker must catch bad responses, or every test that relies on it proves nothing.</summary>
public class ContractDocumentTests
{
    private const string GoodUser = """{"id":"10000000-0000-4000-8000-000000000001","email":"a@b.test","fullName":"A","initials":"A","role":"CLAIMS_REVIEWER"}""";

    [Fact]
    public void Accepts_a_response_that_matches() =>
        Assert.Empty(ContractDocument.Instance.Check("GET", "/me", 200, "application/json", GoodUser));

    [Fact]
    public void Refuses_a_missing_required_property() =>
        Assert.NotEmpty(ContractDocument.Instance.Check("GET", "/me", 200, "application/json", """{"id":"10000000-0000-4000-8000-000000000001"}"""));

    [Fact]
    public void Refuses_an_enum_value_outside_the_list() =>
        Assert.NotEmpty(ContractDocument.Instance.Check(
            "GET",
            "/me",
            200,
            "application/json",
            GoodUser.Replace("CLAIMS_REVIEWER", "ClaimsReviewer", StringComparison.Ordinal)));

    [Fact]
    public void Refuses_a_status_the_operation_does_not_declare() =>
        Assert.NotEmpty(ContractDocument.Instance.Check("GET", "/me", 418, "application/json", "{}"));

    [Fact]
    public void Refuses_a_path_outside_the_contract() =>
        Assert.NotEmpty(ContractDocument.Instance.Check("GET", "/nothing", 200, "application/json", "{}"));
}
