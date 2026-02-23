using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/admin/robots")]
[Route("api/v1/admin/robots")]
[Authorize(Roles = "Admin")]
public class AdminRobotsController(
    IAdminRobotsService adminRobotsService) : ControllerBase
{
    [HttpGet("monitor")]
    public async Task<IActionResult> Monitor([FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        var response = await adminRobotsService.MonitorAsync(take, cancellationToken);
        return Ok(response);
    }

    [HttpPost("run/{robotName}")]
    public async Task<IActionResult> Run(string robotName, CancellationToken cancellationToken = default)
    {
        var result = await adminRobotsService.RunAsync(robotName, cancellationToken);
        if (result is null)
            return NotFound(new { detail = $"Robô '{robotName}' não encontrado." });

        return Ok(result);
    }

    [HttpPost("run-all")]
    public async Task<IActionResult> RunAll(CancellationToken cancellationToken = default)
    {
        var results = await adminRobotsService.RunAllAsync(cancellationToken);
        return Ok(results);
    }
}
