using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/admin/parameters/test-email")]
[Route("api/v1/admin/parameters/test-email")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureAdminParametersManage)]
public class AdminEmailDiagnosticsController(IAdminParametersService adminParametersService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> SendTestEmail([FromBody] SendTestEmailRequest request, CancellationToken cancellationToken)
    {
        var response = await adminParametersService.SendTestEmailAsync(request.To, cancellationToken);
        return Ok(response);
    }
}
