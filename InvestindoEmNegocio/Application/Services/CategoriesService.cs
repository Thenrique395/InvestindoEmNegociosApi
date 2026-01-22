using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

public class CategoriesService(ICategoryRepository categoryRepository, ILogger<CategoriesService> logger) : ICategoriesService
{
    private readonly ILogger<CategoriesService> _logger = logger;
    public async Task<IReadOnlyList<CategoryResponse>> ListAsync(Guid userId, MoneyType? appliesTo, CancellationToken cancellationToken = default)
    {
        var data = await categoryRepository.ListForUserAsync(userId, appliesTo, cancellationToken);
        return data.Select(ToResponse).ToList();
    }

    public async Task<CategoryResponse> CreateAsync(Guid userId, UpsertCategoryRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var name = request.Name.Trim();

        var exists = await categoryRepository.NameExistsAsync(userId, name, null, cancellationToken);
        if (exists) throw new InvalidOperationException("Categoria já existe para o usuário.");

        var category = new Category(userId, name, request.AppliesTo);
        await categoryRepository.AddAsync(category, cancellationToken);
        await categoryRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Category created {UserId} {CategoryId}", userId, category.Id);
        return ToResponse(category);
    }

    public async Task<CategoryResponse?> UpdateAsync(Guid userId, Guid id, UpsertCategoryRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var name = request.Name.Trim();

        var category = await categoryRepository.GetByIdForUserAsync(id, userId, cancellationToken);
        if (category is null) return null;

        var exists = await categoryRepository.NameExistsAsync(userId, name, id, cancellationToken);
        if (exists) throw new InvalidOperationException("Categoria já existe para o usuário.");

        category.Update(name, request.AppliesTo);

        await categoryRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Category updated {UserId} {CategoryId}", userId, category.Id);
        return ToResponse(category);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var category = await categoryRepository.GetByIdForUserAsync(id, userId, cancellationToken);
        if (category is null) return false;

        categoryRepository.Remove(category);
        await categoryRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Category deleted {UserId} {CategoryId}", userId, category.Id);
        return true;
    }

    private static void Validate(UpsertCategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Nome da categoria é obrigatório.");

        if (request.Name.Trim().Length > 60)
            throw new ArgumentException("Nome da categoria deve ter até 60 caracteres.");
    }

    private static CategoryResponse ToResponse(Category c) =>
        new(c.Id, c.Name, c.AppliesTo, c.UserId == null);
}
