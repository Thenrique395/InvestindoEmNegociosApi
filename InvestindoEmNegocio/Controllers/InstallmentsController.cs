using System.Security.Claims;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InstallmentsController(IInstallmentsService installmentsService) : ControllerBase
{
    [HttpGet]
    // Lista parcelas do usuário com filtros opcionais por status, vencimento e tipo (receita/despesa).
    public async Task<IActionResult> List([FromQuery] InstallmentStatus? status, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] MoneyType? type, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var data = await installmentsService.ListAsync(userId, status, from, to, type, cancellationToken);
        return Ok(data);
    }

    [HttpPost("{id:guid}/payments")]
    // Registra um pagamento (ou antecipação) para a parcela e atualiza o status conforme o total pago.
    public async Task<IActionResult> Pay(Guid id, [FromBody] PaymentRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var paid = await installmentsService.PayAsync(userId, id, request, cancellationToken);
        if (!paid) return NotFound();
        return Ok();
    }

    [HttpPost("{id:guid}/anticipations")]
    // Antecipar uma parcela futura: move vencimento para a data informada, marca status e registra data original.
    public async Task<IActionResult> Anticipate(Guid id, [FromBody] AnticipationRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        try
        {
            var anticipated = await installmentsService.AnticipateAsync(userId, id, request, cancellationToken);
            if (!anticipated) return NotFound();
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    // Remove apenas a parcela (e pagamentos) sem excluir o plano inteiro.
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        try
        {
            var removed = await installmentsService.DeleteAsync(userId, id, cancellationToken);
            if (!removed) return NotFound();
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Usuário não autenticado.");
    }
}
