using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/admin/categories")]
[Route("api/v1/admin/categories")]
[Authorize(Roles = "Admin")]
public class AdminCategoriesController(IAdminCategoriesService adminCategoriesService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        var response = await adminCategoriesService.ListAsync(includeInactive, cancellationToken);
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AdminCategoryRequest request, CancellationToken cancellationToken)
    {
        var created = await adminCategoriesService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), created);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AdminCategoryRequest request, CancellationToken cancellationToken)
    {
        var response = await adminCategoriesService.UpdateAsync(id, request, cancellationToken);
        return Ok(response);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateActiveRequest request, CancellationToken cancellationToken)
    {
        var response = await adminCategoriesService.UpdateStatusAsync(id, request.IsActive, cancellationToken);
        return Ok(response);
    }
}
