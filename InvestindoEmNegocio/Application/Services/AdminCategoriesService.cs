using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;

namespace InvestindoEmNegocio.Application.Services;

public sealed class AdminCategoriesService(ICategoryRepository categoryRepository) : IAdminCategoriesService
{
    public async Task<IReadOnlyList<AdminCategoryResponse>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var items = await categoryRepository.ListDefaultsAsync(includeInactive, cancellationToken);
        return items.Select(ToAdminResponse).ToList();
    }

    public async Task<AdminCategoryResponse> CreateAsync(AdminCategoryRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        var name = request.Name.Trim();

        if (await categoryRepository.DefaultNameExistsAsync(name, null, cancellationToken))
        {
            throw new AppProblemException("Categoria já existe", "Já existe uma categoria padrão com esse nome.", StatusCodes.Status409Conflict);
        }

        TryParseAppliesTo(request.AppliesTo, out var appliesTo, out var parseError);
        if (parseError is not null)
        {
            throw new AppProblemException("Categoria inválida", parseError, StatusCodes.Status400BadRequest);
        }

        var category = new Category(null, name, appliesTo);
        await categoryRepository.AddAsync(category, cancellationToken);
        await categoryRepository.SaveChangesAsync(cancellationToken);
        return ToAdminResponse(category);
    }

    public async Task<AdminCategoryResponse> UpdateAsync(Guid id, AdminCategoryRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        var category = await categoryRepository.GetDefaultByIdAsync(id, cancellationToken)
            ?? throw new AppProblemException("Não encontrado", "Categoria não encontrada.", StatusCodes.Status404NotFound);

        var name = request.Name.Trim();
        if (await categoryRepository.DefaultNameExistsAsync(name, id, cancellationToken))
        {
            throw new AppProblemException("Categoria já existe", "Já existe uma categoria padrão com esse nome.", StatusCodes.Status409Conflict);
        }

        TryParseAppliesTo(request.AppliesTo, out var appliesTo, out var parseError);
        if (parseError is not null)
        {
            throw new AppProblemException("Categoria inválida", parseError, StatusCodes.Status400BadRequest);
        }

        category.Update(name, appliesTo);
        await categoryRepository.SaveChangesAsync(cancellationToken);
        return ToAdminResponse(category);
    }

    public async Task<AdminCategoryResponse> UpdateStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var category = await categoryRepository.GetDefaultByIdAsync(id, cancellationToken)
            ?? throw new AppProblemException("Não encontrado", "Categoria não encontrada.", StatusCodes.Status404NotFound);

        if (isActive) category.Activate();
        else category.Deactivate();

        await categoryRepository.SaveChangesAsync(cancellationToken);
        return ToAdminResponse(category);
    }

    private static AdminCategoryResponse ToAdminResponse(Category category) =>
        new(category.Id, category.Name, category.AppliesTo?.ToString(), category.IsActive, category.CreatedAt);

    private static void ValidateRequest(AdminCategoryRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Name))
        {
            throw new AppProblemException("Categoria inválida", "Informe um nome válido.", StatusCodes.Status400BadRequest);
        }

        if (request.Name.Trim().Length > 60)
        {
            throw new AppProblemException("Categoria inválida", "Nome da categoria deve ter até 60 caracteres.", StatusCodes.Status400BadRequest);
        }
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
