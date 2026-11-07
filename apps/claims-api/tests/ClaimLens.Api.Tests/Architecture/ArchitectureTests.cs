using System.Text.RegularExpressions;
using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Features.Lifecycle;
using ClaimLens.Api.Http;
using ClaimLens.RuleEngine;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NetArchTest.Rules;

namespace ClaimLens.Api.Tests.Architecture;

/// <summary>The rules in docs/10-code-structure.md, section 4. More are added as the folders they guard appear.</summary>
public partial class ArchitectureTests
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

    [Fact]
    public void Only_DocumentStore_uses_the_S3_SDK()
    {
        var result = Types.InAssembly(Api)
            .That().DoNotResideInNamespace("ClaimLens.Api.DocumentStore")
            .ShouldNot().HaveDependencyOn("Amazon")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// C3 rule 5: only ClaimStatusMachine changes claim.status. The compiled code is read instruction by instruction, so
    /// a property set, an ExecuteUpdate on Status, or SQL that updates the column anywhere else fails this test.
    /// </summary>
    [Fact]
    public void Only_ClaimStatusMachine_changes_a_claims_status()
    {
        using var module = ModuleDefinition.ReadModule(Api.Location);
        var machine = typeof(ClaimStatusMachine).FullName!;
        var claim = typeof(Claim).FullName!;

        var offenders = module.GetTypes()
            .Where(type => !IsOrIsInside(type, machine))
            .SelectMany(type => type.Methods.Where(method => method.HasBody).Select(method => (type, method)))
            .Where(pair => ChangesStatus(pair.method, claim))
            .Select(pair => $"{pair.type.FullName}.{pair.method.Name}")
            .ToList();

        Assert.Empty(offenders);
    }

    private static bool IsOrIsInside(TypeDefinition type, string name)
    {
        for (var current = type; current is not null; current = current.DeclaringType)
        {
            if (current.FullName == name)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ChangesStatus(MethodDefinition method, string claim)
    {
        var instructions = method.Body.Instructions;
        var setsProperty = instructions.Any(instruction =>
            instruction.Operand is MethodReference { Name: "set_Status" } called && called.DeclaringType.FullName == claim);

        // ExecuteUpdate(s => s.SetProperty(c => c.Status, ...)) compiles to an expression tree that names get_Status.
        var namesStatus = instructions.Any(instruction =>
            instruction.OpCode == OpCodes.Ldtoken
            && instruction.Operand is MethodReference { Name: "get_Status" } token
            && token.DeclaringType.FullName == claim);
        var updatesInBulk = instructions.Any(instruction =>
            instruction.Operand is MethodReference { Name: "SetProperty" });

        var sql = instructions.Any(instruction =>
            instruction.OpCode == OpCodes.Ldstr && StatusUpdateSql().IsMatch((string)instruction.Operand));

        return setsProperty || (namesStatus && updatesInBulk) || sql;
    }

    [GeneratedRegex(@"update\s+(public\.)?""?claim""?\s+set\b[^;]*\bstatus\s*=", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StatusUpdateSql();
}
