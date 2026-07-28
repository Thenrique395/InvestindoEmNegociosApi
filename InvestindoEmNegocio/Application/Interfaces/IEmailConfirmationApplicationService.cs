namespace InvestindoEmNegocio.Application.Interfaces;

public interface IEmailConfirmationApplicationService
{
    Task ConfirmAsync(string token, CancellationToken cancellationToken = default);
    Task ResendAsync(string email, CancellationToken cancellationToken = default);
}
