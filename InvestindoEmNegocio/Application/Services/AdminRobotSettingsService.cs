using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;

namespace InvestindoEmNegocio.Application.Services;

public sealed class AdminRobotSettingsService(IRobotSettingsRepository robotSettingsRepository) : IAdminRobotSettingsService
{
    public async Task<RobotSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await robotSettingsRepository.GetOrCreateAsync(cancellationToken);
        return ToDto(settings);
    }

    public async Task<RobotSettingsDto> UpdateAsync(UpdateRobotSettingsRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DailyRunTimeUtc) || !TimeOnly.TryParse(request.DailyRunTimeUtc, out _))
        {
            throw new AppProblemException(
                "Horário inválido",
                "Informe o horário no formato HH:mm (UTC).",
                StatusCodes.Status400BadRequest);
        }

        var settings = await robotSettingsRepository.GetOrCreateAsync(cancellationToken);
        settings.Update(request.Enabled, request.DailyRunTimeUtc);
        await robotSettingsRepository.SaveChangesAsync(cancellationToken);
        return ToDto(settings);
    }

    private static RobotSettingsDto ToDto(RobotSettings settings) => new(settings.Enabled, settings.DailyRunTimeUtc);
}
