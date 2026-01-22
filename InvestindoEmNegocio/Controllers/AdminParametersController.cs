using System.Linq;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/admin/parameters")]
[Route("api/v1/admin/parameters")]
[Authorize(Roles = "Admin")]
public class AdminParametersController(
    IPaymentMethodRepository paymentMethodRepository,
    ICardBrandRepository cardBrandRepository) : ControllerBase
{
    [HttpGet("payment-methods")]
    public async Task<IActionResult> ListPaymentMethods(CancellationToken cancellationToken)
    {
        var items = await paymentMethodRepository.ListAllAsync(cancellationToken);
        var response = items
            .Select(p => new PaymentMethodAdminResponse(p.Id, p.Name, p.IsActive))
            .ToList();

        return Ok(response);
    }

    [HttpPut("payment-methods/{id:int}/status")]
    public async Task<IActionResult> UpdatePaymentMethodStatus(int id, [FromBody] UpdateActiveRequest request, CancellationToken cancellationToken)
    {
        var method = await paymentMethodRepository.GetByIdAsync(id, cancellationToken);
        if (method is null)
        {
            return NotFound();
        }

        if (request.IsActive)
        {
            method.Activate();
        }
        else
        {
            method.Deactivate();
        }

        await paymentMethodRepository.SaveChangesAsync(cancellationToken);
        return Ok(new PaymentMethodAdminResponse(method.Id, method.Name, method.IsActive));
    }

    [HttpGet("card-brands")]
    public async Task<IActionResult> ListCardBrands(CancellationToken cancellationToken)
    {
        var items = await cardBrandRepository.ListAllAsync(cancellationToken);
        var response = items
            .Select(b => new CardBrandAdminResponse(b.Id, b.Name, b.Code, b.IsActive))
            .ToList();

        return Ok(response);
    }

    [HttpPut("card-brands/{id:int}/status")]
    public async Task<IActionResult> UpdateCardBrandStatus(int id, [FromBody] UpdateActiveRequest request, CancellationToken cancellationToken)
    {
        var brand = await cardBrandRepository.GetByIdAsync(id, cancellationToken);
        if (brand is null)
        {
            return NotFound();
        }

        if (request.IsActive)
        {
            brand.Activate();
        }
        else
        {
            brand.Deactivate();
        }

        await cardBrandRepository.SaveChangesAsync(cancellationToken);
        return Ok(new CardBrandAdminResponse(brand.Id, brand.Name, brand.Code, brand.IsActive));
    }
}
