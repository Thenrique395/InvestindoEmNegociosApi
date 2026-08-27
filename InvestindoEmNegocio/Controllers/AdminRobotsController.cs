using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/admin/robots")]
[Route("api/v1/admin/robots")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureAdminRobotsManage)]
[EnableRateLimiting("admin-robots")]
public class AdminRobotsController(
    IAdminRobotMonitorService adminRobotMonitorService,
    IAdminRobotExecutionService adminRobotExecutionService) : AuthenticatedControllerBase
{
    [HttpGet("monitor")]
    public async Task<IActionResult> Monitor(
        [FromQuery] int take = 50,
        [FromQuery] string? robotName = null,
        [FromQuery] bool? success = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var response = await adminRobotMonitorService.MonitorAsync(
            new RobotMonitorQueryDto(take, robotName, success, from, to, search),
            cancellationToken);
        return Ok(response);
    }

    [HttpPost("run/{robotName}")]
    public async Task<IActionResult> Run(
        string robotName,
        [FromQuery] bool force = false,
        [FromQuery] int cooldownMinutes = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await adminRobotExecutionService.RunAsync(robotName, force, cooldownMinutes, GetUserIdOrNull(), cancellationToken);
        if (result is null)
            throw new AppProblemException(
                "Robô não encontrado",
                $"Robô '{robotName}' não encontrado.",
                StatusCodes.Status404NotFound);

        return Ok(result);
    }

    [HttpPost("run-all")]
    public async Task<IActionResult> RunAll(CancellationToken cancellationToken = default)
    {
        var results = await adminRobotExecutionService.RunAllAsync(GetUserIdOrNull(), cancellationToken);
        return Ok(results);
    }

}
