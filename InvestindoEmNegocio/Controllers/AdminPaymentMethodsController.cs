using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/admin/parameters/payment-methods")]
[Route("api/v1/admin/parameters/payment-methods")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureAdminParametersManage)]
public class AdminPaymentMethodsController(IAdminParametersService adminParametersService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var response = await adminParametersService.ListPaymentMethodsAsync(cancellationToken);
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePaymentMethodRequest request, CancellationToken cancellationToken)
    {
        var response = await adminParametersService.CreatePaymentMethodAsync(request.Name, cancellationToken);
        return Ok(response);
    }

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateActiveRequest request, CancellationToken cancellationToken)
    {
        var response = await adminParametersService.UpdatePaymentMethodStatusAsync(id, request.IsActive, cancellationToken);
        return Ok(response);
    }
}
