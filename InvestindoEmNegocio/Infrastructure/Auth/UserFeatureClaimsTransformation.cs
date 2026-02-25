using System.Security.Claims;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Authentication;

namespace InvestindoEmNegocio.Infrastructure.Auth;

public sealed class UserFeatureClaimsTransformation(
    IUserFeatureOverrideRepository overrideRepository) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
            return principal;

        var userIdRaw = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue(ClaimTypes.Name);
        if (!Guid.TryParse(userIdRaw, out var userId))
            return principal;

        if (identity.HasClaim(c => c.Type == "feature.override.applied"))
            return principal;

        var overrides = await overrideRepository.ListByUserAsync(userId);
        foreach (var item in overrides)
        {
            identity.AddClaim(new Claim(item.IsEnabled ? "feature" : "feature_deny", item.FeatureKey));
        }

        identity.AddClaim(new Claim("feature.override.applied", "1"));
        return principal;
    }
}
