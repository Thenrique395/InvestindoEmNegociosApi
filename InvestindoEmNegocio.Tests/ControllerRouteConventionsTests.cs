using FluentAssertions;
using InvestindoEmNegocio.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Tests;

public class ControllerRouteConventionsTests
{
    [Fact]
    public void FinancialAssistantController_Should_Expose_Canonical_And_Legacy_Routes()
    {
        var routes = GetRoutes(typeof(FinancialAssistantController));

        routes.Should().Contain("api/financial-assistant");
        routes.Should().Contain("api/v1/financial-assistant");
        routes.Should().NotContain("api/financialassistant");
        routes.Should().NotContain("api/v1/financialassistant");
    }

    [Fact]
    public void MonthlyFinancialSnapshotsController_Should_Expose_Canonical_Routes()
    {
        var routes = GetRoutes(typeof(MonthlyFinancialSnapshotsController));

        routes.Should().Contain("api/monthly-snapshots");
        routes.Should().Contain("api/v1/monthly-snapshots");
        routes.Should().NotContain("api/monthlysnapshots");
        routes.Should().NotContain("api/v1/monthlysnapshots");
    }

    [Fact]
    public void DataPortabilityController_Should_Expose_Canonical_And_Legacy_Routes()
    {
        var routes = GetRoutes(typeof(DataPortabilityController));

        routes.Should().Contain("api/data-portability");
        routes.Should().Contain("api/v1/data-portability");
        routes.Should().NotContain("api/dataportability");
        routes.Should().NotContain("api/v1/dataportability");
    }

    [Fact]
    public void InvoiceImportController_Should_Expose_Only_Explicit_Invoice_Import_Routes()
    {
        var routes = GetRoutes(typeof(InvoiceImportController));

        routes.Should().Contain("api/invoice-import");
        routes.Should().Contain("api/v1/invoice-import");
        routes.Should().NotContain("api/[controller]");
        routes.Should().NotContain("api/v1/[controller]");
    }

    private static string[] GetRoutes(Type controllerType) =>
        controllerType
            .GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Select(x => x.Template)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToArray();
}
