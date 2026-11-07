using System.Net;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.DocumentPreparation;
using ClaimLens.Api.Tests.Documents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static ClaimLens.Api.Tests.Features.IntakeData;

namespace ClaimLens.Api.Tests.Features;

/// <summary>
/// A file whose preparation keeps failing for a reason that is not the file's (the converter or storage is down) must
/// not hold up the files behind it, and must end with a reason the officer can act on.
/// </summary>
public sealed class PreparationFailureTests(ApiFactory api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public const string GiveUpReason = "The file could not be processed. Upload it again.";

    [Fact]
    public async Task A_file_that_keeps_failing_does_not_hold_up_the_files_behind_it()
    {
        await api.PrepareDocumentsAsync();
        var client = await api.SignedInAs(Staff.Intake);
        var stuckClaim = await NewDraftAsync(api, client);
        var otherClaim = await NewDraftAsync(api, client);
        var stuck = await UploadedAsync(client, stuckClaim, TestFiles.Docx("Đơn thuốc"), "don-thuoc.docx");
        var photo = await UploadedAsync(client, otherClaim, TestFiles.Jpg(), "bien-lai.jpg");
        await using var down = ConverterDown();

        for (var pass = 0; pass < 5; pass++)
        {
            await PrepareOnceAsync(down);
        }

        Assert.Equal("READY", await StatusOf(photo));
        Assert.Equal("CONVERTING", await StatusOf(stuck));
    }

    [Fact]
    public async Task A_file_that_fails_past_the_attempt_limit_is_rejected_and_the_claim_can_be_submitted()
    {
        await api.PrepareDocumentsAsync();
        var client = await api.SignedInAs(Staff.Intake);
        var claim = await NewDraftAsync(api, client);
        await UploadedAsync(client, claim, TestFiles.Png(), "ready.png");
        await api.PrepareDocumentsAsync();
        var stuck = await UploadedAsync(client, claim, TestFiles.Docx("Đơn thuốc"), "don-thuoc.docx");
        await using var down = ConverterDown(maxAttempts: 3, retryDelaySeconds: 0);

        await PrepareOnceAsync(down);
        await PrepareOnceAsync(down);
        Assert.Equal("UPLOAD_IN_PROGRESS", await Code(await client.PostAsync($"claims/{claim}/submit", null, Cancel)));
        await PrepareOnceAsync(down);

        Assert.Equal("REJECTED", await StatusOf(stuck));
        Assert.Equal(GiveUpReason, await api.Scalar<string>($"select rejection_reason from document where id = '{stuck}'"));
        Assert.Equal(1, await api.Scalar<long>(
            $"select count(*) from audit_event where event_type = 'DOCUMENT_REJECTED' and entity_id = '{stuck}' and actor_type = 'SYSTEM'"));
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync($"claims/{claim}/submit", null, Cancel)).StatusCode);
    }

    [Fact]
    public async Task A_file_left_waiting_can_be_removed_and_the_claim_submitted()
    {
        await api.PrepareDocumentsAsync();
        var client = await api.SignedInAs(Staff.Intake);
        var claim = await NewDraftAsync(api, client);
        await UploadedAsync(client, claim, TestFiles.Png(), "ready.png");
        await api.PrepareDocumentsAsync();
        var stuck = await UploadedAsync(client, claim, TestFiles.Docx("Đơn thuốc"), "don-thuoc.docx");
        await using var down = ConverterDown();
        await PrepareOnceAsync(down);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"claims/{claim}/documents/{stuck}", Cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync($"claims/{claim}/submit", null, Cancel)).StatusCode);
    }

    /// <summary>The same app, with a converter that cannot be reached, as when Gotenberg is down.</summary>
    private WebApplicationFactory<Program> ConverterDown(int? maxAttempts = null, double? retryDelaySeconds = null) =>
        api.WithWebHostBuilder(builder =>
        {
            if (maxAttempts is { } attempts)
            {
                builder.UseSetting("Workers:DocumentPreparation:MaxAttempts", attempts.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            if (retryDelaySeconds is { } delay)
            {
                builder.UseSetting("Workers:DocumentPreparation:RetryDelaySeconds", delay.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDocumentConverter>();
                services.AddSingleton<IDocumentConverter, UnreachableConverter>();
            });
        });

    /// <summary>One pass, as the worker makes it; a failure the worker would log is swallowed here the same way.</summary>
    private static async Task PrepareOnceAsync(WebApplicationFactory<Program> host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        try
        {
            await scope.ServiceProvider.GetRequiredService<DocumentPreparer>().PrepareNextAsync(Cancel);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
        }
    }

    private Task<string?> StatusOf(string document) =>
        api.Scalar<string>($"select status::text from document where id = '{document}'");

    private sealed class UnreachableConverter : IDocumentConverter
    {
        public Task<byte[]> ToPdfAsync(byte[] content, FileFormat format, CancellationToken cancel) =>
            throw new HttpRequestException("Connection refused (converter.test:3000)");
    }
}
