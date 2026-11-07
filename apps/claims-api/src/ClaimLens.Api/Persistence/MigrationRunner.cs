using System.Reflection;
using ClaimLens.Api.Domain.Entities;
using DbUp;
using Microsoft.AspNetCore.Identity;
using Npgsql;

namespace ClaimLens.Api.Persistence;

/// <summary>
/// Applies db/migrations and db/seed, embedded in this assembly. Integration tests call the same code.
/// Migrations are forward-only and run as the owner; the app connects as claimlens_app.
/// </summary>
public static class MigrationRunner
{
    private static readonly Assembly Scripts = typeof(MigrationRunner).Assembly;

    public static int RunCommand(string command, IConfiguration configuration)
    {
        var owner = configuration.GetConnectionString("Owner")
            ?? throw new InvalidOperationException("ConnectionStrings:Owner is not set.");

        if (command == "migrate")
        {
            Migrate(owner, configuration.GetConnectionString("Database"));
            return 0;
        }

        var password = configuration["Seed:DevPassword"]
            ?? throw new InvalidOperationException("Seed:DevPassword is not set, so the dev accounts cannot be created.");
        SeedDevelopment(owner, password);
        return 0;
    }

    public static void Migrate(string ownerConnection, string? appConnection)
    {
        var result = DeployChanges.To
            .PostgresqlDatabase(ownerConnection)
            .WithScriptsEmbeddedInAssembly(Scripts, name => name.StartsWith("migrations.", StringComparison.Ordinal))
            .JournalToPostgresqlTable("public", "schema_migration")
            .WithVariablesDisabled()
            .WithTransactionPerScript()
            .LogToConsole()
            .Build()
            .PerformUpgrade();

        if (!result.Successful)
        {
            throw new InvalidOperationException("A migration failed.", result.Error);
        }

        if (appConnection is not null)
        {
            SetAppPassword(ownerConnection, appConnection);
        }
    }

    /// <summary>Reference data, then the local and test accounts. Each script runs once.</summary>
    public static void SeedDevelopment(string ownerConnection, string devPassword)
    {
        var hash = new PasswordHasher<StaffAccount>().HashPassword(null!, devPassword);

        foreach (var prefix in new[] { "seed.reference.", "seed.dev." })
        {
            var result = DeployChanges.To
                .PostgresqlDatabase(ownerConnection)
                .WithScriptsEmbeddedInAssembly(Scripts, name => name.StartsWith(prefix, StringComparison.Ordinal))
                .JournalToPostgresqlTable("public", "schema_seed")
                .WithVariable("dev_password_hash", hash)
                .WithTransactionPerScript()
                .LogToConsole()
                .Build()
                .PerformUpgrade();

            if (!result.Successful)
            {
                throw new InvalidOperationException("A seed script failed.", result.Error);
            }
        }

        // The seed journal is the runner's bookkeeping, like schema_migration. The owner creates it after 0007, so the
        // default privileges there would hand it to the app.
        using var connection = new NpgsqlConnection(ownerConnection);
        connection.Open();
        using var revoke = new NpgsqlCommand(
            """
            revoke all on schema_seed from claimlens_app;
            do $$
            declare
              journal_sequence text := pg_get_serial_sequence('schema_seed', 'schemaversionsid');
            begin
              if journal_sequence is not null then
                execute format('revoke all on sequence %s from claimlens_app', journal_sequence);
              end if;
            end;
            $$;
            """,
            connection);
        revoke.ExecuteNonQuery();
    }

    private static void SetAppPassword(string ownerConnection, string appConnection)
    {
        var password = new NpgsqlConnectionStringBuilder(appConnection).Password
            ?? throw new InvalidOperationException("ConnectionStrings:Database has no password.");

        using var connection = new NpgsqlConnection(ownerConnection);
        connection.Open();

        // ALTER ROLE takes no parameters, so the password is quoted by the server's own format function.
        using var quote = new NpgsqlCommand("select format('alter role claimlens_app password %L', @password)", connection);
        quote.Parameters.AddWithValue("password", password);
        var statement = (string)quote.ExecuteScalar()!;

        using var alter = new NpgsqlCommand(statement, connection);
        alter.ExecuteNonQuery();
    }
}
