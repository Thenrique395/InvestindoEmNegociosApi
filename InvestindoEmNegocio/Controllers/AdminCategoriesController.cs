using System.Linq;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/admin/categories")]
[Route("api/v1/admin/categories")]
[Authorize(Roles = "Admin")]
public class AdminCategoriesController(ICategoryRepository categoryRepository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        var items = await categoryRepository.ListDefaultsAsync(includeInactive, cancellationToken);
        var response = items
            .Select(ToAdminResponse)
            .ToList();
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AdminCategoryRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Categoria inválida",
                Detail = "Informe um nome válido.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (!TryParseAppliesTo(request.AppliesTo, out var appliesTo, out var error))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Categoria inválida",
                Detail = error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        var name = request.Name.Trim();
        if (name.Length > 60)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Categoria inválida",
                Detail = "Nome da categoria deve ter até 60 caracteres.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var exists = await categoryRepository.DefaultNameExistsAsync(name, null, cancellationToken);
        if (exists)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Categoria já existe",
                Detail = "Já existe uma categoria padrão com esse nome.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var category = new Category(null, name, appliesTo);
        await categoryRepository.AddAsync(category, cancellationToken);
        await categoryRepository.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(List), ToAdminResponse(category));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AdminCategoryRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Categoria inválida",
                Detail = "Informe um nome válido.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (!TryParseAppliesTo(request.AppliesTo, out var appliesTo, out var error))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Categoria inválida",
                Detail = error,
                Status = StatusCodes.Status400BadRequest
            });
        }

        var category = await categoryRepository.GetDefaultByIdAsync(id, cancellationToken);
        if (category is null)
        {
            return NotFound();
        }

        var name = request.Name.Trim();
        if (name.Length > 60)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Categoria inválida",
                Detail = "Nome da categoria deve ter até 60 caracteres.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var exists = await categoryRepository.DefaultNameExistsAsync(name, id, cancellationToken);
        if (exists)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Categoria já existe",
                Detail = "Já existe uma categoria padrão com esse nome.",
                Status = StatusCodes.Status409Conflict
            });
        }

        category.Update(name, appliesTo);
        await categoryRepository.SaveChangesAsync(cancellationToken);
        return Ok(ToAdminResponse(category));
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateActiveRequest request, CancellationToken cancellationToken)
    {
        var category = await categoryRepository.GetDefaultByIdAsync(id, cancellationToken);
        if (category is null)
        {
            return NotFound();
        }

        if (request.IsActive)
        {
            category.Activate();
        }
        else
        {
            category.Deactivate();
        }

        await categoryRepository.SaveChangesAsync(cancellationToken);
        return Ok(ToAdminResponse(category));
    }

    private static AdminCategoryResponse ToAdminResponse(Category category)
    {
        return new AdminCategoryResponse(
            category.Id,
            category.Name,
            category.AppliesTo?.ToString(),
            category.IsActive,
            category.CreatedAt);
    }

    private static bool TryParseAppliesTo(string? value, out MoneyType? appliesTo, out string? error)
    {
        error = null;
        appliesTo = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (Enum.TryParse<MoneyType>(value, true, out var parsed))
        {
            appliesTo = parsed;
            return true;
        }

        error = $"Tipo '{value}' inválido. Use Income ou Expense.";
        return false;
    }
}
