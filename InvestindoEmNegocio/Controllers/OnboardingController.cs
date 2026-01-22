using System.Security.Claims;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[Authorize]
public class OnboardingController(IOnboardingService onboardingService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<OnboardingStatusDto>> Get(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var status = await onboardingService.GetStatusAsync(userId, cancellationToken);
        return Ok(status);
    }

    [HttpPut]
    public async Task<ActionResult<OnboardingStatusDto>> Update([FromBody] UpdateOnboardingRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var status = await onboardingService.UpdateStatusAsync(userId, request, cancellationToken);
        return Ok(status);
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id)
            ? id
            : throw new UnauthorizedAccessException("Usuário não autenticado.");
    }
}
