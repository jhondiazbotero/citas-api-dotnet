using System.Reflection;
using NetArchTest.Rules;

namespace Citas.Architecture.Tests;

public class DependencyRulesTests
{
    private const string Domain = "Citas.Domain";
    private const string Application = "Citas.Application";
    private const string Infrastructure = "Citas.Infrastructure";
    private const string Api = "Citas.Api";
    private const string EntityFrameworkCore = "Microsoft.EntityFrameworkCore";
    private const string AspNetCore = "Microsoft.AspNetCore";

    private static readonly Assembly DomainAssembly = Citas.Domain.AssemblyReference.Assembly;
    private static readonly Assembly ApplicationAssembly = Citas.Application.AssemblyReference.Assembly;

    [Fact]
    public void Domain_should_not_depend_on_other_layers()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(Application, Infrastructure, Api)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
        AssertNoReferences(DomainAssembly, Application, Infrastructure, Api);
    }

    [Fact]
    public void Application_should_not_depend_on_Infrastructure_or_Api()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(Infrastructure, Api)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
        AssertNoReferences(ApplicationAssembly, Infrastructure, Api);
    }

    [Fact]
    public void Application_should_not_depend_on_EntityFrameworkCore_or_AspNetCore()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(EntityFrameworkCore, AspNetCore)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
        AssertNoReferences(ApplicationAssembly, EntityFrameworkCore, AspNetCore);
    }

    // Second safety net: checks assembly references, not just types.
    private static void AssertNoReferences(Assembly assembly, params string[] forbidden)
    {
        var referenced = assembly.GetReferencedAssemblies().Select(a => a.Name).ToHashSet();
        foreach (var name in forbidden)
        {
            Assert.DoesNotContain(name, referenced);
        }
    }

    private static string Describe(NetArchTest.Rules.TestResult result) =>
        "Types violating the rule: " + string.Join(", ", result.FailingTypeNames ?? []);
}
