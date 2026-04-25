using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/billing/portal")]
[Route("api/v1/billing/portal")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureBillingManage)]
public class BillingPortalController(IBillingPortalService billingPortalService) : AuthenticatedControllerBase
{
    [HttpPost]
    public async Task<ActionResult<BillingPortalSessionResponse>> Create(CancellationToken cancellationToken)
    {
        var response = await billingPortalService.CreatePortalSessionAsync(GetUserId(), cancellationToken);
        return Ok(response);
    }
}
