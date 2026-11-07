using System.Text.Json;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Http;
using ClaimLens.RuleEngine;
using Npgsql;

namespace ClaimLens.Api.Tests.Persistence;

/// <summary>The rules that live in the database, run against the same migrations as production.</summary>
public class DatabaseTests(ApiFactory api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private const string Member = "30000000-0000-4000-8000-000000000001";
    private const string Policy = "31000000-0000-4000-8000-000000000001";

    [Fact]
    public async Task All_21_tables_exist() =>
        Assert.Equal(21, await api.Scalar<long>(
            """
            select count(*) from information_schema.tables
            where table_schema = 'public' and table_name not in ('schema_migration', 'schema_seed')
            """));

    [Fact]
    public async Task Every_constraint_in_the_error_map_exists()
    {
        var names = await Names(
            """
            select conname from pg_constraint where connamespace = 'public'::regnamespace
            union select indexname from pg_indexes where schemaname = 'public'
            """);
        var triggerSource = await api.Scalar<string>("select string_agg(prosrc, ' ') from pg_proc where pronamespace = 'public'::regnamespace");

        var missing = ConstraintErrorMap.All.Keys
            .Where(name => !names.Contains(name) && !triggerSource!.Contains(name, StringComparison.Ordinal))
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void Every_code_in_the_error_map_is_in_the_contract()
    {
        var contract = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "contract", "07-api-contract.md"));

        Assert.All(ConstraintErrorMap.All.Values, mapped => Assert.Contains($"`{mapped.Code}`", contract, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Csharp_enums_match_the_database_values()
    {
        var database = await EnumValues();
        var enums = typeof(ClaimStatus).Assembly.GetTypes()
            .Where(type => type.IsEnum && type.Namespace == typeof(ClaimStatus).Namespace)
            .Concat([typeof(LineResult), typeof(RuleType), typeof(LimitScope)])
            .ToList();

        Assert.Equal(36, enums.Count);

        foreach (var type in enums)
        {
            var dbName = JsonNamingPolicy.SnakeCaseLower.ConvertName(type.Name);
            var policy = type == typeof(FieldKey) ? JsonNamingPolicy.SnakeCaseLower : JsonNamingPolicy.SnakeCaseUpper;
            var expected = Enum.GetNames(type).Select(policy.ConvertName).Order(StringComparer.Ordinal);

            Assert.Equal(database[dbName].Order(StringComparer.Ordinal), expected);
        }
    }

    [Fact]
    public async Task The_app_role_cannot_rewrite_the_audit_trail()
    {
        await using var connection = new NpgsqlConnection(api.AppConnection);
        await connection.OpenAsync(Cancel);
        await using var delete = new NpgsqlCommand("delete from audit_event", connection);

        var error = await Assert.ThrowsAsync<PostgresException>(() => delete.ExecuteNonQueryAsync(Cancel));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    [Fact]
    public async Task The_app_role_can_write_the_audit_trail()
    {
        await using var connection = new NpgsqlConnection(api.AppConnection);
        await connection.OpenAsync(Cancel);
        await using var insert = new NpgsqlCommand(
            "insert into audit_event (actor_type, event_type, entity_type, entity_id) values ('SYSTEM', 'TEST', 'test', '1')",
            connection);

        Assert.Equal(1, await insert.ExecuteNonQueryAsync(Cancel));
    }

    [Fact]
    public async Task The_app_role_cannot_touch_the_migration_journal()
    {
        await using var connection = new NpgsqlConnection(api.AppConnection);
        await connection.OpenAsync(Cancel);
        await using var read = new NpgsqlCommand("select count(*) from schema_migration", connection);

        await Assert.ThrowsAsync<PostgresException>(() => read.ExecuteScalarAsync(Cancel));
    }

    [Theory]
    [InlineData("update benefit_rule set clause_text = 'x' where rule_code = 'EX-04'", "ck_rule_set_frozen")]
    [InlineData("update rule_set_version set status = 'DRAFT' where version_no = 3", "ck_rule_set_frozen")]
    [InlineData("delete from benefit_rule where rule_code = 'EX-04'", "ck_rule_set_frozen")]
    [InlineData("update policy set consumed_amount = consumed_amount + 1 where policy_number = 'PL-2026-00731'", "ck_policy_usage_in_step")]
    [InlineData("update policy set status = 'LAPSED' where policy_number = 'PL-2026-00731'", "ck_policy_terminated_on")]
    [InlineData("update policy set terminated_on = '2026-06-01' where policy_number = 'PL-2026-00731'", "ck_policy_terminated_on")]
    public async Task Guards_refuse_with_a_named_constraint(string sql, string constraint)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() => api.Sql(sql));

        Assert.Equal(constraint, error.ConstraintName);
    }

    // Q-A (docs/11-audit-and-roadmap.md): a policy that is no longer active says from when.
    [Theory]
    [InlineData("LAPSED")]
    [InlineData("TERMINATED")]
    public async Task A_policy_that_is_not_active_keeps_the_date_it_ended(string status)
    {
        var id = Guid.CreateVersion7();
        await api.Sql(
            $"""
            insert into policy (id, policy_number, member_id, plan_id, status, coverage_start, coverage_end, first_enrolled_on, terminated_on)
            values ('{id}', 'T-{id.ToString("N")[20..]}', '{Member}', '20000000-0000-4000-8000-000000000001', '{status}',
                    '2026-01-01', '2026-12-31', '2026-01-01', '2026-06-01')
            """);

        Assert.Equal(new DateOnly(2026, 6, 1), await api.Scalar<DateOnly>($"select terminated_on from policy where id = '{id}'"));
    }

    [Fact]
    public async Task A_claim_cannot_skip_a_status()
    {
        var id = Guid.CreateVersion7();
        await api.Sql(
            $"""
            insert into claim (id, claim_number, member_id, policy_id, provider_name, service_date, claimed_total, channel, registered_by)
            values ('{id}', 'S-{id.ToString("N")[20..]}', '{Member}', '{Policy}', 'x', '2026-10-12', 1, 'COUNTER', '{Staff.IntakeId}')
            """);

        var error = await Assert.ThrowsAsync<PostgresException>(() => api.Sql($"update claim set status = 'APPROVED' where id = '{id}'"));

        Assert.Equal("ck_claim_lifecycle", error.ConstraintName);
    }

    [Fact]
    public async Task A_claim_cannot_reach_a_final_status_without_a_decision()
    {
        var id = Guid.CreateVersion7();
        await api.Sql(
            $"""
            insert into claim (id, claim_number, member_id, policy_id, provider_name, service_date, claimed_total, channel, registered_by)
            values ('{id}', 'D-{id.ToString("N")[20..]}', '{Member}', '{Policy}', 'x', '2026-10-12', 1, 'COUNTER', '{Staff.IntakeId}');
            update claim set status = 'PROCESSING' where id = '{id}';
            update claim set status = 'READY_FOR_REVIEW' where id = '{id}';
            update claim set status = 'IN_REVIEW', assignee_id = '{Staff.ReviewerId}', assigned_at = now() where id = '{id}';
            update claim set status = 'PENDING_APPROVAL' where id = '{id}';
            """);

        var error = await Assert.ThrowsAsync<PostgresException>(() => api.Sql($"update claim set status = 'APPROVED' where id = '{id}'"));

        Assert.Equal("ck_claim_final_needs_decision", error.ConstraintName);
    }

    [Fact]
    public async Task Vietnamese_names_are_found_without_accents()
    {
        var found = await api.Scalar<string>(
            """
            select full_name from member
            where immutable_unaccent(full_name) ilike '%' || immutable_unaccent('pham thi bich') || '%'
            """);

        Assert.Equal("Phạm Thị Bích Ngọc", found);
    }

    [Fact]
    public async Task The_seeded_rule_set_is_published_and_complete() =>
        Assert.Equal("PUBLISHED", await api.Scalar<string>("select status::text from rule_set_version where version_no = 3"));

    private async Task<HashSet<string>> Names(string sql)
    {
        await using var connection = new NpgsqlConnection(api.OwnerConnection);
        await connection.OpenAsync(Cancel);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(Cancel);
        var names = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(Cancel))
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private async Task<Dictionary<string, List<string>>> EnumValues()
    {
        await using var connection = new NpgsqlConnection(api.OwnerConnection);
        await connection.OpenAsync(Cancel);
        await using var command = new NpgsqlCommand(
            "select t.typname, e.enumlabel from pg_enum e join pg_type t on t.oid = e.enumtypid",
            connection);
        await using var reader = await command.ExecuteReaderAsync(Cancel);
        var values = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        while (await reader.ReadAsync(Cancel))
        {
            var name = reader.GetString(0);
            if (!values.TryGetValue(name, out var list))
            {
                values[name] = list = [];
            }

            list.Add(reader.GetString(1));
        }

        return values;
    }
}
