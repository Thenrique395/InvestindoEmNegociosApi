using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Application.Services;

public sealed class AdminPaymentMethodsService(IPaymentMethodRepository paymentMethodRepository) : IAdminPaymentMethodsService
{
    public async Task<IReadOnlyList<PaymentMethodAdminResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = await paymentMethodRepository.ListAllAsync(cancellationToken);
        return items.Select(p => new PaymentMethodAdminResponse(p.Id, p.Name, p.IsActive)).ToList();
    }

    public async Task<PaymentMethodAdminResponse> UpdateStatusAsync(int id, bool isActive, CancellationToken cancellationToken = default)
    {
        var method = await paymentMethodRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppProblemException("Forma de pagamento não encontrada", "Forma de pagamento não encontrada.", StatusCodes.Status404NotFound);

        if (isActive) method.Activate();
        else method.Deactivate();

        await paymentMethodRepository.SaveChangesAsync(cancellationToken);
        return new PaymentMethodAdminResponse(method.Id, method.Name, method.IsActive);
    }

    public async Task<PaymentMethodAdminResponse> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new AppProblemException("Nome inválido", "Informe o nome da forma de pagamento.", StatusCodes.Status400BadRequest);
        }

        var existing = await paymentMethodRepository.ListAllAsync(cancellationToken);
        if (existing.Any(p => string.Equals(p.Name, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AppProblemException("Forma de pagamento já existe", "Já existe uma forma de pagamento com esse nome.", StatusCodes.Status409Conflict);
        }

        var nextId = existing.Count == 0 ? 1 : existing.Max(p => p.Id) + 1;
        var method = new PaymentMethod(nextId, normalized, true);

        try
        {
            await paymentMethodRepository.AddAsync(method, cancellationToken);
            await paymentMethodRepository.SaveChangesAsync(cancellationToken);
            return new PaymentMethodAdminResponse(method.Id, method.Name, method.IsActive);
        }
        catch (DbUpdateException)
        {
            throw new AppProblemException(
                "Falha ao salvar",
                "Não foi possível salvar a forma de pagamento. Verifique se já existe um registro parecido.",
                StatusCodes.Status409Conflict);
        }
    }
}
