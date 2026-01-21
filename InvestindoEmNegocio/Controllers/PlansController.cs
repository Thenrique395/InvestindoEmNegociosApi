using System.Security.Claims;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PlansController : ControllerBase
{
    private readonly IPlansService _plansService;
    private readonly IAuditService _auditService;

    public PlansController(IPlansService plansService, IAuditService auditService)
    {
        _plansService = plansService;
        _auditService = auditService;
    }

    [HttpPost]
    // Cria um plano de receita/despesa e gera parcelas conforme o tipo (à vista, parcelado ou recorrente).
    public async Task<ActionResult<PlanResponse>> Create([FromBody] CreatePlanRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        try
        {
            var plan = await _plansService.CreateAsync(userId, request, cancellationToken);
            return Ok(plan);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
                { Title = "Plano inválido", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
        }
    }

    [HttpGet]
    // Lista planos do usuário, com filtro opcional por tipo (receita ou despesa).
    public async Task<IActionResult> List([FromQuery] MoneyType? type, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var data = await _plansService.ListAsync(userId, type, cancellationToken);
        return Ok(data);
    }

    [HttpGet("{id:guid}")]
    // Retorna um plano específico com suas parcelas geradas.
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var data = await _plansService.GetByIdAsync(userId, id, cancellationToken);
        if (data is null) return NotFound();
        return Ok(data);
    }

    [HttpPut("{id:guid}")]
    // Atualiza um plano (receita/despesa) e regenera parcelas conforme o tipo.
    public async Task<ActionResult<PlanResponse>> Update(Guid id, [FromBody] CreatePlanRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        try
        {
            var plan = await _plansService.UpdateAsync(userId, id, request, cancellationToken);
            if (plan is null) return NotFound();
            return Ok(plan);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
                { Title = "Plano inválido", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
        }
    }

    [HttpDelete("{id:guid}")]
    // Remove um plano (e cascata remove parcelas e pagamentos).
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var removed = await _plansService.DeleteAsync(userId, id, cancellationToken);
        if (!removed) return NotFound();
        await _auditService.LogAsync(userId, "DELETE", "Plan", id.ToString(), GetIpAddress(), GetUserAgent(), null, cancellationToken);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id)
            ? id
            : throw new UnauthorizedAccessException("Usuário não autenticado.");
    }

    private string? GetIpAddress()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private string? GetUserAgent()
    {
        return Request.Headers["User-Agent"].ToString();
    }
}
