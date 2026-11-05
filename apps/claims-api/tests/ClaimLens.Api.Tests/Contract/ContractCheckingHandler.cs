namespace ClaimLens.Api.Tests.Contract;

/// <summary>Sits in the test client, so every response any test sees is checked against the contract.</summary>
public sealed class ContractCheckingHandler : DelegatingHandler
{
    private const string BasePath = "/api/v1";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        // 501 is a contract operation whose feature is not built yet.
        if ((int)response.StatusCode == 501)
        {
            return response;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var path = request.RequestUri!.AbsolutePath[BasePath.Length..];
        var problems = ContractDocument.Instance.Check(
            request.Method.Method,
            path,
            (int)response.StatusCode,
            response.Content.Headers.ContentType?.MediaType,
            body);

        Assert.True(
            problems.Count == 0,
            $"{request.Method} {path} answered {(int)response.StatusCode}, which breaks the contract:\n{string.Join('\n', problems)}\n{body}");

        return response;
    }
}
