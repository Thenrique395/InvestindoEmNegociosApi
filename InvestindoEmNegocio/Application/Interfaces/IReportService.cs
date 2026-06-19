using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IReportService
{
    Task<MonthlySummaryReportResponse> GetMonthlySummaryAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default);
}
