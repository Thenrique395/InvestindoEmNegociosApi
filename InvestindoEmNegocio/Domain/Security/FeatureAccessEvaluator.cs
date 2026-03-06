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
        var roleClaimValue =
            user.FindFirstValue(ClaimTypes.Role) ??
            user.FindFirstValue("role") ??
            user.FindFirstValue("http://schemas.microsoft.com/ws/2008/06/identity/claims/role");

        if (!Enum.TryParse<UserRole>(roleClaimValue, ignoreCase: true, out var currentRole))
            return null;

        return currentRole;
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
