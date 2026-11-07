using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using ClaimLens.Api.DocumentPreparation;
using ClaimLens.Api.DocumentStore;
using ClaimLens.Api.Tests.Contract;
using ClaimLens.Api.Tests.Documents;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkiaSharp;
using static ClaimLens.Api.Tests.Features.IntakeData;

namespace ClaimLens.Api.Tests.Integration;

/// <summary>SeaweedFS and Gotenberg as in infra/compose.yaml, the same images, started once for this class.</summary>
public sealed class DocumentServices : IAsyncLifetime
{
    public const string Bucket = "claimlens";
    public const string AccessKey = "test-claims-api";
    public const string SecretKey = "test-claims-api-secret";

    private const string IdentityConfig = $$"""
        {
          "identities": [
            {
              "name": "claims-api",
              "credentials": [{ "accessKey": "{{AccessKey}}", "secretKey": "{{SecretKey}}" }],
              "actions": ["Admin:{{Bucket}}", "Read:{{Bucket}}", "Write:{{Bucket}}", "List:{{Bucket}}", "Tagging:{{Bucket}}"]
            }
          ]
        }
        """;

    private readonly IContainer storage = new ContainerBuilder("chrislusf/seaweedfs:4.52")
        .WithCommand("server", "-dir=/data", "-volume.max=5", "-s3", "-s3.port=8333", "-s3.config=/etc/seaweedfs/s3.json")
        .WithResourceMapping(Encoding.UTF8.GetBytes(IdentityConfig), "/etc/seaweedfs/s3.json")
        .WithPortBinding(8333, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request
            .ForPort(8333)
            .ForPath("/")
            .ForStatusCodeMatching(status => (int)status is >= 200 and < 500)))
        .Build();

    private readonly IContainer converter = new ContainerBuilder("gotenberg/gotenberg:8.39.0")
        .WithCommand("gotenberg", "--api-timeout=60s")
        .WithPortBinding(3000, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(3000).ForPath("/health")))
        .Build();

    public string StorageUrl => $"http://{storage.Hostname}:{storage.GetMappedPublicPort(8333)}";

    public string ConverterUrl => $"http://{converter.Hostname}:{converter.GetMappedPublicPort(3000)}";

    public async ValueTask InitializeAsync() => await Task.WhenAll(storage.StartAsync(), converter.StartAsync());

    public async ValueTask DisposeAsync()
    {
        await storage.DisposeAsync();
        await converter.DisposeAsync();
    }

    /// <summary>A client of the store itself, to see what reached it.</summary>
    public AmazonS3Client S3() =>
        new(
            new BasicAWSCredentials(AccessKey, SecretKey),
            new AmazonS3Config
            {
                ServiceURL = StorageUrl,
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            });
}

/// <summary>
/// US03 end to end on the real converter and object store, with the background worker running as it does in the
/// stack: Vietnamese DOCX and XLSX files, a photo with an EXIF rotation, and a renamed file posing as a DOCX.
/// </summary>
public sealed class DocumentPipelineTests : IClassFixture<DocumentServices>, IAsyncDisposable
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static readonly TimeSpan PreparationTimeout = TimeSpan.FromSeconds(90);

    private readonly ApiFactory api;
    private readonly DocumentServices services;
    private readonly WebApplicationFactory<Program> host;

    public DocumentPipelineTests(ApiFactory api, DocumentServices services)
    {
        this.api = api;
        this.services = services;
        host = api.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Storage:ServiceUrl", services.StorageUrl);
            builder.UseSetting("Storage:Bucket", DocumentServices.Bucket);
            builder.UseSetting("Storage:AccessKey", DocumentServices.AccessKey);
            builder.UseSetting("Storage:SecretKey", DocumentServices.SecretKey);
            builder.UseSetting("Storage:CreateBucket", "true");
            builder.UseSetting("Converter:BaseUrl", services.ConverterUrl);
            builder.UseSetting("Workers:DocumentPreparation:Enabled", "true");
            builder.UseSetting("Workers:DocumentPreparation:PollIntervalSeconds", "0.2");
            builder.ConfigureTestServices(replaced =>
            {
                // Back to what Startup registers, in place of the in-memory store and the fake converter.
                replaced.RemoveAll<IDocumentStore>();
                replaced.AddSingleton<IDocumentStore, S3DocumentStore>();
                replaced.RemoveAll<IDocumentConverter>();
                replaced.AddHttpClient<IDocumentConverter, ConverterClient>();
            });
        });
    }

    public async ValueTask DisposeAsync() => await host.DisposeAsync();

    [Fact]
    public async Task A_Vietnamese_DOCX_is_kept_and_its_PDF_rendition_gives_one_page_per_printed_page()
    {
        var (client, claim) = await NewDraftAsync();
        var docx = TestFiles.Docx(
            "ĐƠN THUỐC — Phòng khám Đa khoa Hồng Ngọc",
            "Họ tên: Nguyễn Văn An. Chẩn đoán: Viêm họng cấp.",
            "Paracetamol 500mg × 10 viên: 35.000 ₫",
            null,
            "Trang 2 — Lời dặn: uống sau ăn, ngày 2 lần.");

        var document = await UploadAsync(client, claim, docx, "Đơn thuốc Nguyễn Văn An.docx");
        var ready = await ReadyAsync(client, claim, document);

        Assert.Equal("DOCX", ready.GetProperty("fileFormat").GetString());
        Assert.Equal(2, ready.GetProperty("pages").GetArrayLength());
        Assert.Equal(docx, await ObjectAsync($"originals/{claim}/{document}.docx"));
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString((await ObjectAsync($"renditions/{claim}/{document}.pdf"))![..4]), StringComparison.Ordinal);
        Assert.All(await KeysAsync(claim), key => Assert.DoesNotContain("Nguy", key, StringComparison.Ordinal));
        Assert.True(HasInk(await PageAsync(client, claim, ready, 0)), "The first page should show the prescription's text.");
    }

    [Fact]
    public async Task A_Vietnamese_XLSX_is_kept_and_its_worksheet_becomes_pages()
    {
        var (client, claim) = await NewDraftAsync();
        var xlsx = TestFiles.Xlsx(
            "Bảng kê chi phí",
            ["STT", "Dịch vụ", "Số lượng", "Thành tiền (₫)"],
            [1L, "Khám chuyên khoa Tai Mũi Họng", 1L, 300_000L],
            [2L, "Nội soi tai mũi họng", 1L, 250_000L],
            [3L, "Kem dưỡng phục hồi da", 1L, 450_000L]);

        var document = await UploadAsync(client, claim, xlsx, "bang-ke.xlsx");
        var ready = await ReadyAsync(client, claim, document);

        Assert.Equal("XLSX", ready.GetProperty("fileFormat").GetString());
        Assert.True(ready.GetProperty("pages").GetArrayLength() >= 1);
        Assert.NotNull(await ObjectAsync($"renditions/{claim}/{document}.pdf"));
        Assert.True(HasInk(await PageAsync(client, claim, ready, 0)), "The worksheet page should show the lines.");
    }

    [Fact]
    public async Task A_photo_with_an_EXIF_rotation_is_stored_upright()
    {
        var (client, claim) = await NewDraftAsync();

        var document = await UploadAsync(client, claim, TestFiles.Jpg(400, 200, orientation: 6), "bien-lai.jpg");
        var ready = await ReadyAsync(client, claim, document);

        var page = ready.GetProperty("pages")[0];
        Assert.Equal((200, 400), (page.GetProperty("widthPx").GetInt32(), page.GetProperty("heightPx").GetInt32()));
        using var image = SKBitmap.Decode(await PageAsync(client, claim, ready, 0));
        Assert.True(image.GetPixel(100, 100).Red > 200, "Red belongs on top once the photo is upright.");
        Assert.True(image.GetPixel(100, 300).Blue > 200, "Blue belongs at the bottom once the photo is upright.");
        Assert.NotNull(await ObjectAsync(StorageKeys.ThumbnailOf($"pages/{claim}/{document}/1.png")));
    }

    [Fact]
    public async Task A_file_posing_as_a_DOCX_is_refused_before_anything_is_stored()
    {
        var (client, claim) = await NewDraftAsync();

        var response = await RawUploadAsync(client, claim, TestFiles.FakeDocx(), "don-thuoc.docx");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("UNSUPPORTED_FILE_TYPE", await Code(response));
        Assert.Empty(await KeysAsync(claim));
        Assert.Equal(0, await api.Scalar<long>($"select count(*) from document where claim_id = '{claim}'"));
    }

    private async Task<(HttpClient Client, string Claim)> NewDraftAsync()
    {
        var client = host.CreateDefaultClient(new Uri("https://localhost/api/v1/"), new ContractCheckingHandler());
        var signIn = await client.PostAsync(
            "auth/login",
            new StringContent(JsonSerializer.Serialize(new { email = Staff.Intake, password = ApiFactory.Password }), Encoding.UTF8, "application/json"),
            Cancel);
        signIn.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", (await JsonOf(signIn)).GetProperty("accessToken").GetString());

        return (client, await Features.IntakeData.NewDraftAsync(api, client));
    }

    private static async Task<string> UploadAsync(HttpClient client, string claim, byte[] content, string name)
    {
        var response = await RawUploadAsync(client, claim, content, name);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return Id(await JsonOf(response));
    }

    private static Task<HttpResponseMessage> RawUploadAsync(HttpClient client, string claim, byte[] content, string name) =>
        Features.IntakeData.UploadAsync(client, claim, content, name);

    /// <summary>Polls getClaimStatus as the web app does, until the worker has finished with the file.</summary>
    private static async Task<JsonElement> ReadyAsync(HttpClient client, string claim, string document)
    {
        var deadline = DateTime.UtcNow + PreparationTimeout;
        while (true)
        {
            var status = (await JsonOf(await client.GetAsync($"claims/{claim}/status", Cancel)))
                .GetProperty("documents").EnumerateArray()
                .Single(row => Id(row) == document);
            var state = status.GetProperty("status").GetString();
            if (state != "CONVERTING")
            {
                Assert.True(state == "READY", $"The file ended {state}: {status.GetProperty("rejectionReason")}");
                return (await DocumentsAsync(client, claim)).Single(row => Id(row) == document);
            }

            Assert.True(DateTime.UtcNow < deadline, "The worker did not prepare the file in time.");
            await Task.Delay(TimeSpan.FromMilliseconds(250), Cancel);
        }
    }

    private static async Task<byte[]> PageAsync(HttpClient client, string claim, JsonElement document, int index)
    {
        var page = Id(document.GetProperty("pages")[index]);
        var response = await client.GetAsync($"claims/{claim}/pages/{page}/image", Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsByteArrayAsync(Cancel);
    }

    /// <summary>A rendered page with text on it has dark pixels; a blank one has none.</summary>
    private static bool HasInk(byte[] png)
    {
        using var bitmap = SKBitmap.Decode(png);
        for (var y = 0; y < bitmap.Height; y += 2)
        {
            for (var x = 0; x < bitmap.Width; x += 2)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Red < 100 && pixel.Green < 100 && pixel.Blue < 100)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private async Task<byte[]?> ObjectAsync(string key)
    {
        using var s3 = services.S3();
        try
        {
            using var response = await s3.GetObjectAsync(DocumentServices.Bucket, key, Cancel);
            using var copy = new MemoryStream();
            await response.ResponseStream.CopyToAsync(copy, Cancel);
            return copy.ToArray();
        }
        catch (AmazonS3Exception error) when (error.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>Every key in the bucket that belongs to this claim, under any prefix.</summary>
    private async Task<List<string>> KeysAsync(string claim)
    {
        using var s3 = services.S3();
        var keys = new List<string>();
        foreach (var prefix in new[] { "originals/", "renditions/", "pages/", "thumbnails/" })
        {
            var listed = await s3.ListObjectsV2Async(
                new ListObjectsV2Request { BucketName = DocumentServices.Bucket, Prefix = $"{prefix}{claim}/" },
                Cancel);
            keys.AddRange((listed.S3Objects ?? []).Select(item => item.Key));
        }

        return keys;
    }
}
