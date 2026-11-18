using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace InvestindoEmNegocio.Infrastructure.Auth;

public class CurrentSpaceAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentSpaceAccessor, ISpaceExecutionScope
{
    private readonly AsyncLocal<Guid?> backgroundSpace = new();

    public Guid? SpaceId
    {
        get
        {
            if (backgroundSpace.Value is Guid scoped) return scoped;
            var claim = httpContextAccessor.HttpContext?.User?.FindFirst(JwtTokenGenerator.SpaceIdClaim)?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public IDisposable BeginScope(Guid spaceId)
    {
        if (spaceId == Guid.Empty) throw new ArgumentException("Espaço inválido.", nameof(spaceId));
        var previous = backgroundSpace.Value;
        backgroundSpace.Value = spaceId;
        return new Scope(() => backgroundSpace.Value = previous);
    }

    private sealed class Scope(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }

    public Guid RequireSpaceId() =>
        SpaceId ?? throw new UnauthorizedAccessException("Espaço ativo não encontrado.");
}
