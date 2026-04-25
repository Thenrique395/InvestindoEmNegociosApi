using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Application.Services;

public sealed class AdminCardBrandsService(ICardBrandRepository cardBrandRepository) : IAdminCardBrandsService
{
    public async Task<IReadOnlyList<CardBrandAdminResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = await cardBrandRepository.ListAllAsync(cancellationToken);
        return items.Select(b => new CardBrandAdminResponse(b.Id, b.Name, b.Code, b.IsActive)).ToList();
    }

    public async Task<CardBrandAdminResponse> UpdateStatusAsync(int id, bool isActive, CancellationToken cancellationToken = default)
    {
        var brand = await cardBrandRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppProblemException("Bandeira não encontrada", "Bandeira não encontrada.", StatusCodes.Status404NotFound);

        if (isActive) brand.Activate();
        else brand.Deactivate();

        await cardBrandRepository.SaveChangesAsync(cancellationToken);
        return new CardBrandAdminResponse(brand.Id, brand.Name, brand.Code, brand.IsActive);
    }

    public async Task<CardBrandAdminResponse> CreateAsync(CreateCardBrandRequest request, CancellationToken cancellationToken = default)
    {
        var name = (request.Name ?? string.Empty).Trim();
        var code = (request.Code ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
        {
            throw new AppProblemException("Dados inválidos", "Informe o nome e o código da bandeira.", StatusCodes.Status400BadRequest);
        }

        var existing = await cardBrandRepository.ListAllAsync(cancellationToken);
        if (existing.Any(b => string.Equals(b.Code, code, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AppProblemException("Código já existe", "Já existe uma bandeira com esse código.", StatusCodes.Status409Conflict);
        }

        var nextId = existing.Count == 0 ? 1 : existing.Max(b => b.Id) + 1;
        var brand = new CardBrand(nextId, name, code, true);

        try
        {
            await cardBrandRepository.AddAsync(brand, cancellationToken);
            await cardBrandRepository.SaveChangesAsync(cancellationToken);
            return new CardBrandAdminResponse(brand.Id, brand.Name, brand.Code, brand.IsActive);
        }
        catch (DbUpdateException)
        {
            throw new AppProblemException(
                "Falha ao salvar",
                "Não foi possível salvar a bandeira. Verifique se o código já está em uso.",
                StatusCodes.Status409Conflict);
        }
    }
}
