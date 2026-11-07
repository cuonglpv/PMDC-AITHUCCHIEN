namespace ClaimLens.Api.DocumentStore;

/// <summary>
/// Reads and writes objects by key. The only component that knows where files live; the rest of the API holds keys,
/// which never reach a response (docs/07-api-contract.md).
/// </summary>
public interface IDocumentStore
{
    Task PutAsync(string key, byte[] content, string contentType, CancellationToken cancel);

    /// <summary>The object's bytes, or null when no object has this key.</summary>
    Task<byte[]?> GetAsync(string key, CancellationToken cancel);

    /// <summary>Deleting a key that does not exist is not an error.</summary>
    Task DeleteAsync(IReadOnlyCollection<string> keys, CancellationToken cancel);
}
