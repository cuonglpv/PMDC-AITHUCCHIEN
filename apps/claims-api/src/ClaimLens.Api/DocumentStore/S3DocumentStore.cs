using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace ClaimLens.Api.DocumentStore;

/// <summary>
/// Object storage through the S3 API only, so SeaweedFS locally and any S3 service later need no code change (S12).
/// The only type in the API that uses the S3 SDK; an architecture test holds the rest to that.
/// </summary>
public sealed class S3DocumentStore : IDocumentStore, IDisposable
{
    private readonly AmazonS3Client client;
    private readonly string bucket;

    public S3DocumentStore(IOptions<StorageOptions> options)
    {
        var storage = options.Value;
        bucket = storage.Bucket;
        client = new AmazonS3Client(
            new BasicAWSCredentials(storage.AccessKey, storage.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = storage.ServiceUrl,
                ForcePathStyle = true,
                AuthenticationRegion = storage.Region,
                Timeout = TimeSpan.FromSeconds(storage.TimeoutSeconds),
                MaxErrorRetry = 2,
                // S3-compatible stores do not all know the newer default checksums; send them only where S3 requires one.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            });
    }

    public async Task PutAsync(string key, byte[] content, string contentType, CancellationToken cancel)
    {
        using var body = new MemoryStream(content, writable: false);
        await client.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = body,
                ContentType = contentType,
                AutoCloseStream = false,
            },
            cancel);
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken cancel)
    {
        try
        {
            using var response = await client.GetObjectAsync(new GetObjectRequest { BucketName = bucket, Key = key }, cancel);
            using var copy = new MemoryStream();
            await response.ResponseStream.CopyToAsync(copy, cancel);
            return copy.ToArray();
        }
        catch (AmazonS3Exception error) when (error.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(IReadOnlyCollection<string> keys, CancellationToken cancel)
    {
        // One request per key: a multi-object delete needs a checksum header that not every S3-compatible store accepts,
        // and a claim has at most 30 pages.
        foreach (var key in keys)
        {
            await client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = bucket, Key = key }, cancel);
        }
    }

    /// <summary>Local and test setups only (Storage:CreateBucket). An existing bucket is fine.</summary>
    public async Task CreateBucketAsync(CancellationToken cancel)
    {
        try
        {
            await client.PutBucketAsync(new PutBucketRequest { BucketName = bucket }, cancel);
        }
        catch (AmazonS3Exception error) when (error.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
        {
        }
    }

    public void Dispose() => client.Dispose();
}

/// <summary>Creates the bucket when the API starts, if Storage:CreateBucket asks for it. Storage may still be starting.</summary>
public sealed class BucketSetup(IServiceProvider services, IOptions<StorageOptions> options, ILogger<BucketSetup> logger) : IHostedService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);
    private const int Attempts = 15;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.CreateBucket || services.GetRequiredService<IDocumentStore>() is not S3DocumentStore store)
        {
            return;
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await store.CreateBucketAsync(cancellationToken);
                logger.LogInformation("Bucket {Bucket} is ready", options.Value.Bucket);
                return;
            }
            catch (Exception error) when (attempt < Attempts && error is AmazonServiceException or HttpRequestException or IOException)
            {
                logger.LogWarning("Storage is not reachable yet ({Message}); attempt {Attempt} of {Attempts}", error.Message, attempt, Attempts);
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
