using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAdminEmailDiagnosticsService
{
    Task<TestEmailResult> SendTestEmailAsync(string to, CancellationToken cancellationToken = default);
}
