using System.Linq;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

    [HttpPost("payment-methods")]
    public async Task<IActionResult> CreatePaymentMethod([FromBody] CreatePaymentMethodRequest request, CancellationToken cancellationToken)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new ProblemDetails { Title = "Nome inválido", Detail = "Informe o nome da forma de pagamento.", Status = StatusCodes.Status400BadRequest });
        }

        var existing = await paymentMethodRepository.ListAllAsync(cancellationToken);
        if (existing.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return Conflict(new ProblemDetails { Title = "Forma já existe", Detail = "Já existe uma forma de pagamento com esse nome.", Status = StatusCodes.Status409Conflict });
        }

        var nextId = existing.Count == 0 ? 1 : existing.Max(p => p.Id) + 1;
        var method = new PaymentMethod(nextId, name, true);
        try
        {
            await paymentMethodRepository.AddAsync(method, cancellationToken);
            await paymentMethodRepository.SaveChangesAsync(cancellationToken);
            return Ok(new PaymentMethodAdminResponse(method.Id, method.Name, method.IsActive));
        }
        catch (DbUpdateException)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Falha ao salvar",
                Detail = "Não foi possível salvar a forma de pagamento. Verifique se já existe um registro parecido.",
                Status = StatusCodes.Status409Conflict
            });
        }
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

    [HttpPost("card-brands")]
    public async Task<IActionResult> CreateCardBrand([FromBody] CreateCardBrandRequest request, CancellationToken cancellationToken)
    {
        var name = (request.Name ?? string.Empty).Trim();
        var code = (request.Code ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Dados inválidos",
                Detail = "Informe nome e código da bandeira.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var existing = await cardBrandRepository.ListAllAsync(cancellationToken);
        if (existing.Any(b => string.Equals(b.Code, code, StringComparison.OrdinalIgnoreCase)))
        {
            return Conflict(new ProblemDetails { Title = "Código já existe", Detail = "Já existe uma bandeira com esse código.", Status = StatusCodes.Status409Conflict });
        }

        var nextId = existing.Count == 0 ? 1 : existing.Max(b => b.Id) + 1;
        var brand = new CardBrand(nextId, name, code, true);
        try
        {
            await cardBrandRepository.AddAsync(brand, cancellationToken);
            await cardBrandRepository.SaveChangesAsync(cancellationToken);
            return Ok(new CardBrandAdminResponse(brand.Id, brand.Name, brand.Code, brand.IsActive));
        }
        catch (DbUpdateException)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Falha ao salvar",
                Detail = "Não foi possível salvar a bandeira. Verifique se o código já está em uso.",
                Status = StatusCodes.Status409Conflict
            });
        }
    }
}
