using System.Reflection;
using NetArchTest.Rules;

namespace InvestindoEmNegocio.Tests;

public class CleanArchitectureRulesTests
{
    private static readonly Assembly BackendAssembly = typeof(InvestindoEmNegocio.Application.Services.AuthService).Assembly;

    [Fact]
    public void Domain_Should_Not_Depend_On_Outer_Layers()
    {
        var result = Types
            .InAssembly(BackendAssembly)
            .That()
            .ResideInNamespace("InvestindoEmNegocio.Domain")
            .ShouldNot()
            .HaveDependencyOnAny(
                "InvestindoEmNegocio.Application",
                "InvestindoEmNegocio.Infrastructure",
                "InvestindoEmNegocio.Controllers")
            .GetResult();

        AssertArchitecture(result);
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure()
    {
        var result = Types
            .InAssembly(BackendAssembly)
            .That()
            .ResideInNamespace("InvestindoEmNegocio.Application")
            .ShouldNot()
            .HaveDependencyOn("InvestindoEmNegocio.Infrastructure")
            .GetResult();

        AssertArchitecture(result);
    }

    [Fact]
    public void Controllers_Should_Not_Depend_On_DbContext()
    {
        var result = Types
            .InAssembly(BackendAssembly)
            .That()
            .ResideInNamespace("InvestindoEmNegocio.Controllers")
            .ShouldNot()
            .HaveDependencyOn("InvestindoEmNegocio.Infrastructure.Data")
            .GetResult();

        AssertArchitecture(result);
    }

    private static void AssertArchitecture(TestResult result)
    {
        if (result.IsSuccessful)
        {
            return;
        }

        var failingTypes = result.FailingTypeNames is { Count: > 0 }
            ? string.Join(", ", result.FailingTypeNames)
            : "sem tipos listados";

        Assert.Fail($"Regra de arquitetura quebrada: {failingTypes}");
    }
}
