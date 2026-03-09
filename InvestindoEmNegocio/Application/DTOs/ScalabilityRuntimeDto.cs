namespace InvestindoEmNegocio.Application.DTOs;

public record ScalabilityRuntimeDto(
    string CurrentPhase,
    IReadOnlyList<string> EnabledControls,
    IReadOnlyList<string> NextPhaseTargets,
    IReadOnlyList<string> LongTermTargets);
