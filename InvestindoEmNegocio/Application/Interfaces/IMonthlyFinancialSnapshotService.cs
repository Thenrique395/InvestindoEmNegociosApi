using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IMonthlyFinancialSnapshotService
{
    Task<IReadOnlyList<MonthlyFinancialSnapshotResponse>> ListAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<MonthlyFinancialSnapshotResponse> GenerateAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default);
}
