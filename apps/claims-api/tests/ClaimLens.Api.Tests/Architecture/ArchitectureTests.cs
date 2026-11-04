using ClaimLens.Api.Http;
using ClaimLens.RuleEngine;
using NetArchTest.Rules;

namespace ClaimLens.Api.Tests.Architecture;

/// <summary>The rules in docs/10-code-structure.md, section 4. More are added as the folders they guard appear.</summary>
public class ArchitectureTests
{
    private static readonly System.Reflection.Assembly Engine = typeof(RuleEvaluator).Assembly;
    private static readonly System.Reflection.Assembly Api = typeof(OperationPolicy).Assembly;

    [Fact]
    public void The_rule_engine_depends_on_nothing_of_ours_and_nothing_that_does_io()
    {
        var result = Types.InAssembly(Engine)
            .ShouldNot()
            .HaveDependencyOnAny(
                "ClaimLens.Api",
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "Microsoft.AspNetCore",
                "System.Net.Http",
                "System.IO")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void The_rule_engine_project_references_only_the_framework()
    {
        var ours = Engine.GetReferencedAssemblies()
            .Where(reference => reference.Name!.StartsWith("ClaimLens", StringComparison.Ordinal));

        Assert.Empty(ours);
    }

    [Fact]
    public void Only_Persistence_uses_the_migration_library()
    {
        var result = Types.InAssembly(Api)
            .That().DoNotResideInNamespace("ClaimLens.Api.Persistence")
            .ShouldNot().HaveDependencyOn("DbUp")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void The_domain_does_not_depend_on_HTTP_or_the_database()
    {
        var result = Types.InAssembly(Api)
            .That().ResideInNamespace("ClaimLens.Api.Domain")
            .ShouldNot().HaveDependencyOnAny("Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Npgsql")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }
}
