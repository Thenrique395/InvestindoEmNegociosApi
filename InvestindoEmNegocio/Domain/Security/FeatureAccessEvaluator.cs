using System.Security.Claims;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Security;

public static class FeatureAccessEvaluator
{
    public static IReadOnlyCollection<string> GetRoleFeatures(UserRole role)
        => AppFeatureMatrix.GetRoleFeatures(role);

    public static bool HasFeature(ClaimsPrincipal user, string featureKey)
    {
        if (string.IsNullOrWhiteSpace(featureKey))
            return false;

        var role = ResolveRole(user);
        if (role is null)
            return false;

        if (role == UserRole.Admin)
            return true;

        var explicitDenied = ResolveExplicitDeniedFeatures(user);
        if (explicitDenied.Contains(featureKey))
            return false;

        var explicitFeatures = ResolveExplicitFeatures(user);
        if (explicitFeatures.Contains(featureKey))
            return true;

        return AppFeatureMatrix.HasRoleFeature(role.Value, featureKey);
    }

    public static UserRole? ResolveRole(ClaimsPrincipal user)
    {
        foreach (var roleClaimValue in GetRoleClaimValues(user))
        {
            if (TryParseRole(roleClaimValue, out var currentRole))
                return currentRole;

            if (roleClaimValue is null)
                continue;

            foreach (var candidate in roleClaimValue.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (TryParseRole(candidate, out var parsedRole))
                    return parsedRole;
            }
        }

        return null;
    }

    private static IEnumerable<string?> GetRoleClaimValues(ClaimsPrincipal user)
    {
        yield return user.FindFirstValue(ClaimTypes.Role);
        yield return user.FindFirstValue("role");
        yield return user.FindFirstValue("roles");
        yield return user.FindFirstValue("http://schemas.microsoft.com/ws/2008/06/identity/claims/role");
    }

    private static bool TryParseRole(string? roleClaimValue, out UserRole role)
    {
        if (string.IsNullOrWhiteSpace(roleClaimValue))
        {
            role = default;
            return false;
        }

        return Enum.TryParse<UserRole>(roleClaimValue, ignoreCase: true, out role);
    }

    private static HashSet<string> ResolveExplicitFeatures(ClaimsPrincipal user)
    {
        var values = new List<string>();

        values.AddRange(user.FindAll("feature").Select(c => c.Value));
        values.AddRange(user.FindAll("features").SelectMany(c =>
            c.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)));

        return values.Count == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> ResolveExplicitDeniedFeatures(ClaimsPrincipal user)
    {
        var values = user.FindAll("feature_deny").Select(c => c.Value).ToList();
        return values.Count == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
    }
}
