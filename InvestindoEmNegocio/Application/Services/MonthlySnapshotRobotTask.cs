using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

public sealed class MonthlySnapshotRobotTask(
    IUserRepository userRepository,
    IMonthlyFinancialSnapshotRepository snapshotRepository,
    IMonthlyFinancialSnapshotService snapshotService,
    ILogger<MonthlySnapshotRobotTask> logger) : IRobotTask
{
    public string Name => "RoboSnapshotMensal";

    public async Task<RobotTaskExecutionResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var prevYear = now.Month == 1 ? now.Year - 1 : now.Year;
        var prevMonth = now.Month == 1 ? 12 : now.Month - 1;

        var allUsers = await userRepository.ListAsync(cancellationToken);
        var eligible = allUsers
            .Where(u => u.Role is UserRole.Intermediate or UserRole.Advanced or UserRole.Admin)
            .ToList();

        logger.LogInformation("{Name}: verificando snapshot de {Prev:yyyy-MM} para {Count} usuários elegíveis",
            Name, new DateOnly(prevYear, prevMonth, 1), eligible.Count);

        if (eligible.Count == 0)
            return new RobotTaskExecutionResult(0, ZeroItemsReasonCode: "NO_ELIGIBLE_USERS");

        var generated = 0;

        foreach (var user in eligible)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var existing = await snapshotRepository.GetByMonthAsync(user.Id, prevYear, prevMonth, cancellationToken);
            if (existing is not null)
                continue;

            try
            {
                await snapshotService.GenerateAsync(user.Id, prevYear, prevMonth, cancellationToken);
                generated++;
                logger.LogInformation("{Name}: snapshot gerado para usuário {UserId} ({Year}-{Month:D2})",
                    Name, user.Id, prevYear, prevMonth);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "{Name}: falha ao gerar snapshot para usuário {UserId} ({Year}-{Month:D2})",
                    Name, user.Id, prevYear, prevMonth);
            }
        }

        logger.LogInformation("{Name}: {Generated} snapshots gerados de {Total} usuários elegíveis",
            Name, generated, eligible.Count);

        return new RobotTaskExecutionResult(generated,
            ZeroItemsReasonCode: generated == 0 ? "ALL_SNAPSHOTS_ALREADY_EXIST" : null);
    }
}
