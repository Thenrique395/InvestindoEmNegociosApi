namespace InvestindoEmNegocio.Application.Interfaces;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, string? textBody = null, CancellationToken cancellationToken = default);
}
