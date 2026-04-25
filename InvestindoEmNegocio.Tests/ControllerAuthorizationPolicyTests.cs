using System.Reflection;
using FluentAssertions;
using InvestindoEmNegocio.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace InvestindoEmNegocio.Tests;

public class ControllerAuthorizationPolicyTests
{
    private static readonly Assembly BackendAssembly = typeof(AuthController).Assembly;

    [Fact]
    public void Private_Controller_Actions_Should_Use_Feature_Policies()
    {
        var violations = BackendAssembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(GetHttpActions)
            .Where(action => !HasAllowAnonymous(action.ControllerType, action.Method))
            .Select(action => new
            {
                Action = action,
                Policies = GetAuthorizeAttributes(action.ControllerType, action.Method)
                    .Select(attribute => attribute.Policy)
                    .ToArray()
            })
            .Where(action => action.Policies.Length == 0
                || action.Policies.Any(policy => string.IsNullOrWhiteSpace(policy) || !policy.StartsWith("feature.", StringComparison.Ordinal)))
            .Select(action => $"{action.Action.ControllerType.Name}.{action.Action.Method.Name}: {FormatPolicies(action.Policies)}")
            .ToArray();

        violations.Should().BeEmpty("all private controller actions must be guarded by explicit feature policies");
    }

    private static IEnumerable<(Type ControllerType, MethodInfo Method)> GetHttpActions(Type controllerType)
        => controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
            .Select(method => (controllerType, method));

    private static bool HasAllowAnonymous(Type controllerType, MethodInfo method)
        => controllerType.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any()
            || method.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any();

    private static IEnumerable<AuthorizeAttribute> GetAuthorizeAttributes(Type controllerType, MethodInfo method)
        => controllerType.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(method.GetCustomAttributes<AuthorizeAttribute>(inherit: true));

    private static string FormatPolicies(IReadOnlyCollection<string?> policies)
        => policies.Count == 0
            ? "missing [Authorize]"
            : string.Join(", ", policies.Select(policy => string.IsNullOrWhiteSpace(policy) ? "<empty>" : policy));
}
