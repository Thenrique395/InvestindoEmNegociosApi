using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace InvestindoEmNegocio.Infrastructure.Auth;

public class CurrentSpaceAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentSpaceAccessor
{
    public Guid? SpaceId
    {
        get
        {
            var claim = httpContextAccessor.HttpContext?.User?.FindFirst(JwtTokenGenerator.SpaceIdClaim)?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public Guid RequireSpaceId() =>
        SpaceId ?? throw new UnauthorizedAccessException("Espaço ativo não encontrado.");
}
