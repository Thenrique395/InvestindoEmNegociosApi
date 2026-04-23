using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/onboarding")]
[Route("api/v1/onboarding")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureOnboardingManage)]
public class OnboardingController(IOnboardingService onboardingService) : AuthenticatedControllerBase
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

}
