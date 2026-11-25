using System.Reflection;
using NetArchTest.Rules;

namespace Citas.Architecture.Tests;

public class DependencyRulesTests
{
    private const string Domain = "Citas.Domain";
    private const string Application = "Citas.Application";
    private const string Infrastructure = "Citas.Infrastructure";
    private const string Api = "Citas.Api";

    private static readonly Assembly DomainAssembly = Citas.Domain.AssemblyReference.Assembly;
    private static readonly Assembly ApplicationAssembly = Citas.Application.AssemblyReference.Assembly;

    [Fact]
    public void Domain_no_depende_de_otras_capas()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(Application, Infrastructure, Api)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
        AssertNoReferences(DomainAssembly, Application, Infrastructure, Api);
    }

    [Fact]
    public void Application_no_depende_de_Infrastructure_ni_Api()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(Infrastructure, Api)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
        AssertNoReferences(ApplicationAssembly, Infrastructure, Api);
    }

    // Segunda red de seguridad: mira las referencias de ensamblado, no solo los tipos.
    private static void AssertNoReferences(Assembly assembly, params string[] forbidden)
    {
        var referenced = assembly.GetReferencedAssemblies().Select(a => a.Name).ToHashSet();
        foreach (var name in forbidden)
        {
            Assert.DoesNotContain(name, referenced);
        }
    }

    private static string Describe(TestResult result) =>
        "Tipos que violan la regla: " + string.Join(", ", result.FailingTypeNames ?? []);
}
