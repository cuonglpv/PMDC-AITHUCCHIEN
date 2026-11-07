using System.Collections.Concurrent;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.DocumentPreparation;
using ClaimLens.Api.DocumentStore;

namespace ClaimLens.Api.Tests.Documents;

/// <summary>Object storage in memory, so most tests need no SeaweedFS. One integration class runs the real one.</summary>
public sealed class InMemoryDocumentStore : IDocumentStore
{
    private readonly ConcurrentDictionary<string, (byte[] Content, string ContentType)> objects = new(StringComparer.Ordinal);

    /// <summary>Makes every write fail, as when storage is down.</summary>
    public bool FailWrites { get; set; }

    public IReadOnlyCollection<string> Keys => objects.Keys.ToList();

    public byte[]? this[string key] => objects.TryGetValue(key, out var stored) ? stored.Content : null;

    public string? ContentTypeOf(string key) => objects.TryGetValue(key, out var stored) ? stored.ContentType : null;

    public Task PutAsync(string key, byte[] content, string contentType, CancellationToken cancel)
    {
        if (FailWrites)
        {
            throw new IOException("Storage is down (test).");
        }

        objects[key] = (content.ToArray(), contentType);
        return Task.CompletedTask;
    }

    public Task<byte[]?> GetAsync(string key, CancellationToken cancel) => Task.FromResult(this[key]);

    public Task DeleteAsync(IReadOnlyCollection<string> keys, CancellationToken cancel)
    {
        foreach (var key in keys)
        {
            objects.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public void Remove(string key) => objects.TryRemove(key, out _);
}

/// <summary>Stands in for Gotenberg: answers a PDF of as many pages as asked, or refuses the file.</summary>
public sealed class FakeConverter : IDocumentConverter
{
    public int Pages { get; set; } = 1;

    /// <summary>When set, the next conversions fail with this reason, as a file LibreOffice cannot open would.</summary>
    public string? FailWith { get; set; }

    public List<FileFormat> Calls { get; } = [];

    public void Reset()
    {
        Pages = 1;
        FailWith = null;
        Calls.Clear();
    }

    public Task<byte[]> ToPdfAsync(byte[] content, FileFormat format, CancellationToken cancel)
    {
        Calls.Add(format);
        return FailWith is { } reason
            ? throw new ConversionFailedException(reason)
            : Task.FromResult(TestFiles.Pdf(Pages));
    }
}
