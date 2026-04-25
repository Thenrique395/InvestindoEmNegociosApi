namespace InvestindoEmNegocio.Application.Interfaces;

public interface INotificationGenerationService
{
    Task<int> GenerateAsync(Guid userId, CancellationToken cancellationToken = default);
}
