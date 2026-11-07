using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClaimLens.Api.AiJobs;
using ClaimLens.Api.DocumentPreparation;
using ClaimLens.Api.DocumentStore;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using ClaimLens.Api.Tests.AiJobs;
using ClaimLens.Api.Tests.Contract;
using ClaimLens.Api.Tests.Documents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(ClaimLens.Api.Tests.ApiFactory))]

namespace ClaimLens.Api.Tests;

/// <summary>The real app on a real PostgreSQL, migrated and seeded by the same code as `migrate` and `seed`.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "test-only-password";
    public const string Origin = "http://web.test";

    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("claimlens")
        .WithUsername("claimlens_owner")
        .WithPassword("test-owner")
        .Build();

    public TestClock Clock { get; } = new();

    /// <summary>Object storage for every test but the one integration class that runs SeaweedFS.</summary>
    public InMemoryDocumentStore Store { get; } = new();

    /// <summary>Gotenberg for every test but the one integration class that runs it.</summary>
    public FakeConverter Converter { get; } = new();

    /// <summary>The Document AI Service for every test.</summary>
    public FakeDocumentAi Ai { get; } = new();

    public string OwnerConnection => database.GetConnectionString();

    public string AppConnection =>
        new NpgsqlConnectionStringBuilder(OwnerConnection) { Username = "claimlens_app", Password = "test-app" }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await database.StartAsync();
        MigrationRunner.Migrate(OwnerConnection, AppConnection);
        MigrationRunner.SeedDevelopment(OwnerConnection, Password);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await database.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.UseSetting("ConnectionStrings:Database", AppConnection);
        builder.UseSetting("Auth:SigningKey", "test-only-signing-key-long-enough-for-hs256");
        builder.UseSetting("Auth:AllowedOrigins:0", Origin);
        builder.UseSetting("Auth:SignInFailuresPerAddress", "1000");

        // Valid settings for services the tests replace below; nothing connects to these addresses.
        builder.UseSetting("Storage:ServiceUrl", "http://storage.test");
        builder.UseSetting("Storage:AccessKey", "test");
        builder.UseSetting("Storage:SecretKey", "test");
        builder.UseSetting("Converter:BaseUrl", "http://converter.test");
        builder.UseSetting("Ai:BaseUrl", "http://document-ai.test");
        builder.UseSetting("Ai:ServiceKey", "test-service-key");

        // Tests prepare documents themselves, with PrepareDocumentsAsync, so they know when it has happened.
        builder.UseSetting("Workers:DocumentPreparation:Enabled", "false");

        // The same for AI runs, with RunAiJobsAsync.
        builder.UseSetting("Workers:AiJobs:Enabled", "false");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<IDocumentStore>();
            services.AddSingleton<IDocumentStore>(Store);
            services.RemoveAll<IDocumentConverter>();
            services.AddSingleton<IDocumentConverter>(Converter);
            services.RemoveAll<IDocumentAiClient>();
            services.AddSingleton<IDocumentAiClient>(Ai);
        });
    }

    /// <summary>Runs Document Preparation until no file is waiting, as the worker would. Returns how many it handled.</summary>
    public async Task<int> PrepareDocumentsAsync(IServiceProvider? services = null)
    {
        var handled = 0;
        while (true)
        {
            await using var scope = (services ?? Services).CreateAsyncScope();
            if (!await scope.ServiceProvider.GetRequiredService<DocumentPreparer>().PrepareNextAsync(TestContext.Current.CancellationToken))
            {
                return handled;
            }

            handled++;
        }
    }

    /// <summary>
    /// Runs the AI Job Runner until no run is waiting, as the worker would. A run waiting out a failure is left for a
    /// later call. Returns how many passes handled a run.
    /// </summary>
    public async Task<int> RunAiJobsAsync(IServiceProvider? services = null)
    {
        for (var handled = 0; handled < 500; handled++)
        {
            await using var scope = (services ?? Services).CreateAsyncScope();
            if (!await scope.ServiceProvider.GetRequiredService<AiRunProcessor>().RunNextAsync(TestContext.Current.CancellationToken))
            {
                return handled;
            }
        }

        throw new InvalidOperationException("The AI Job Runner did not run out of work.");
    }

    /// <summary>Every response through this client is checked against claims-api.yaml.</summary>
    public HttpClient Client()
    {
        var client = CreateDefaultClient(new Uri("https://localhost/api/v1/"), new ContractCheckingHandler());
        return client;
    }

    public async Task<HttpClient> SignedInAs(string email)
    {
        var client = Client();
        var response = await client.PostAsJsonAsync("auth/login", new { email, password = Password }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<SessionBody>(TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        return client;
    }

    public async Task Sql(string sql)
    {
        await using var connection = new NpgsqlConnection(OwnerConnection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    public async Task<T?> Scalar<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(OwnerConnection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return value is DBNull ? default : (T?)value;
    }

    /// <summary>The first column of every row.</summary>
    public async Task<List<T>> Column<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(OwnerConnection);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var values = new List<T>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            values.Add(reader.GetFieldValue<T>(0));
        }

        return values;
    }

    public sealed record SessionBody(string AccessToken, string TokenType, int ExpiresIn);
}

/// <summary>Time the tests can move, for token rotation, sign-in windows and dates that must not be in the past.</summary>
public sealed class TestClock : TimeProvider
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private TimeSpan offset;

    public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + offset;

    public void Advance(TimeSpan by) => offset += by;

    /// <summary>Moves to this moment; time keeps running from there.</summary>
    public void Set(DateTimeOffset now) => offset = now - System.GetUtcNow();

    /// <summary>Moves to 09:00 on this date in Vietnam, where business dates are kept.</summary>
    public void SetToday(DateOnly date) => Set(new DateTimeOffset(date.ToDateTime(new TimeOnly(9, 0)), VietnamOffset));

    /// <summary>Back to the real time.</summary>
    public void Reset() => offset = TimeSpan.Zero;
}

/// <summary>The seeded accounts in db/seed/dev/020_staff.sql.</summary>
public static class Staff
{
    public const string Intake = "intake@claimlens.test";
    public const string Reviewer = "ha.tran@claimlens.test";
    public const string OtherReviewer = "duc.pham@claimlens.test";
    public const string Approver = "quan.le@claimlens.test";
    public const string Admin = "admin@claimlens.test";

    public const string IntakeId = "10000000-0000-4000-8000-000000000001";

    /// <summary>A second Intake Officer that tests insert when they need one; it cannot sign in.</summary>
    public const string OtherIntakeId = "10000000-0000-4000-8000-0000000000a1";
    public const string ReviewerId = "10000000-0000-4000-8000-000000000002";
}

public sealed record ProblemBody(string Code, string Title, int Status, string RequestId);
