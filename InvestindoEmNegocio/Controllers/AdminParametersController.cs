using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/admin/parameters")]
[Route("api/v1/admin/parameters")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureAdminParametersManage)]
public class AdminParametersController(IAdminParametersService adminParametersService) : ControllerBase
{
    [HttpGet("payment-methods")]
    public async Task<IActionResult> ListPaymentMethods(CancellationToken cancellationToken)
    {
        var response = await adminParametersService.ListPaymentMethodsAsync(cancellationToken);
        return Ok(response);
    }

    [HttpPut("payment-methods/{id:int}/status")]
    public async Task<IActionResult> UpdatePaymentMethodStatus(int id, [FromBody] UpdateActiveRequest request, CancellationToken cancellationToken)
    {
        var response = await adminParametersService.UpdatePaymentMethodStatusAsync(id, request.IsActive, cancellationToken);
        return Ok(response);
    }

    [HttpPost("payment-methods")]
    public async Task<IActionResult> CreatePaymentMethod([FromBody] CreatePaymentMethodRequest request, CancellationToken cancellationToken)
    {
        var response = await adminParametersService.CreatePaymentMethodAsync(request.Name, cancellationToken);
        return Ok(response);
    }

    [HttpGet("card-brands")]
    public async Task<IActionResult> ListCardBrands(CancellationToken cancellationToken)
    {
        var response = await adminParametersService.ListCardBrandsAsync(cancellationToken);
        return Ok(response);
    }

    [HttpPut("card-brands/{id:int}/status")]
    public async Task<IActionResult> UpdateCardBrandStatus(int id, [FromBody] UpdateActiveRequest request, CancellationToken cancellationToken)
    {
        var response = await adminParametersService.UpdateCardBrandStatusAsync(id, request.IsActive, cancellationToken);
        return Ok(response);
    }

    [HttpPost("card-brands")]
    public async Task<IActionResult> CreateCardBrand([FromBody] CreateCardBrandRequest request, CancellationToken cancellationToken)
    {
        var response = await adminParametersService.CreateCardBrandAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpGet("institutions")]
    public async Task<IActionResult> ListInstitutions(CancellationToken cancellationToken)
    {
        var response = await adminParametersService.ListInstitutionsAsync(cancellationToken);
        return Ok(response);
    }

    [HttpPost("institutions")]
    public async Task<IActionResult> CreateInstitution([FromBody] CreateInstitutionRequest request, CancellationToken cancellationToken)
    {
        var response = await adminParametersService.CreateInstitutionAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPut("institutions/{id:int}/status")]
    public async Task<IActionResult> UpdateInstitutionStatus(int id, [FromBody] UpdateActiveRequest request, CancellationToken cancellationToken)
    {
        var response = await adminParametersService.UpdateInstitutionStatusAsync(id, request.IsActive, cancellationToken);
        return Ok(response);
    }

    [HttpGet("notification-settings")]
    public async Task<IActionResult> GetNotificationSettings(CancellationToken cancellationToken)
    {
        var response = await adminParametersService.GetNotificationSettingsAsync(cancellationToken);
        return Ok(response);
    }

    [HttpPut("notification-settings")]
    public async Task<IActionResult> UpdateNotificationSettings([FromBody] UpdateNotificationSettingsRequest request, CancellationToken cancellationToken)
    {
        var response = await adminParametersService.UpdateNotificationSettingsAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpGet("robot-settings")]
    public async Task<IActionResult> GetRobotSettings(CancellationToken cancellationToken)
    {
        var response = await adminParametersService.GetRobotSettingsAsync(cancellationToken);
        return Ok(response);
    }

    [HttpPut("robot-settings")]
    public async Task<IActionResult> UpdateRobotSettings([FromBody] UpdateRobotSettingsRequest request, CancellationToken cancellationToken)
    {
        var response = await adminParametersService.UpdateRobotSettingsAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("test-email")]
    public async Task<IActionResult> SendTestEmail([FromBody] SendTestEmailRequest request, CancellationToken cancellationToken)
    {
        var response = await adminParametersService.SendTestEmailAsync(request.To, cancellationToken);
        return Ok(response);
    }
}
