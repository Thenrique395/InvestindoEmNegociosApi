using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Application.Services;

public sealed class AdminInstitutionsService(IInstitutionRepository institutionRepository) : IAdminInstitutionsService
{
    public async Task<IReadOnlyList<InstitutionAdminResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = await institutionRepository.ListAllAsync(cancellationToken);
        return items.Select(i => new InstitutionAdminResponse(i.Id, i.Name, i.Type.ToString(), i.IsActive)).ToList();
    }

    public async Task<InstitutionAdminResponse> CreateAsync(CreateInstitutionRequest request, CancellationToken cancellationToken = default)
    {
        var name = (request.Name ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(request.Type))
        {
            throw new AppProblemException("Dados inválidos", "Informe o nome e o tipo da instituição.", StatusCodes.Status400BadRequest);
        }

        if (!Enum.TryParse<InstitutionType>(request.Type, true, out var type))
        {
            throw new AppProblemException("Tipo inválido", "O tipo informado não é válido.", StatusCodes.Status400BadRequest);
        }

        if (await institutionRepository.ExistsAsync(name, type, cancellationToken))
        {
            throw new AppProblemException("Instituição já existe", "Já existe uma instituição com esse nome e tipo.", StatusCodes.Status409Conflict);
        }

        var institution = new Institution(name, type, true);
        try
        {
            await institutionRepository.AddAsync(institution, cancellationToken);
            await institutionRepository.SaveChangesAsync(cancellationToken);
            return new InstitutionAdminResponse(institution.Id, institution.Name, institution.Type.ToString(), institution.IsActive);
        }
        catch (DbUpdateException)
        {
            throw new AppProblemException(
                "Falha ao salvar",
                "Não foi possível salvar a instituição. Verifique se já existe um registro parecido.",
                StatusCodes.Status409Conflict);
        }
    }

    public async Task<InstitutionAdminResponse> UpdateStatusAsync(int id, bool isActive, CancellationToken cancellationToken = default)
    {
        var institution = await institutionRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppProblemException("Instituição não encontrada", "Instituição não encontrada.", StatusCodes.Status404NotFound);

        if (isActive) institution.Activate();
        else institution.Deactivate();

        await institutionRepository.SaveChangesAsync(cancellationToken);
        return new InstitutionAdminResponse(institution.Id, institution.Name, institution.Type.ToString(), institution.IsActive);
    }
}
