using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/admin/parameters/scalability-runtime")]
[Route("api/v1/admin/parameters/scalability-runtime")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureAdminParametersManage)]
public class AdminRuntimeController(IAdminRuntimeInfoService adminRuntimeInfoService) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(adminRuntimeInfoService.Get());
    }
}
