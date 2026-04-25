using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;

namespace InvestindoEmNegocio.Application.Services;

public sealed class AdminRuntimeInfoService : IAdminRuntimeInfoService
{
    public ScalabilityRuntimeDto Get()
    {
        return new ScalabilityRuntimeDto(
            "phase-1-runtime-hardened",
            [
                "memory cache",
                "rate limiter",
                "response compression",
                "OpenTelemetry + Serilog",
                "HTTP resilience policies",
                "background workers"
            ],
            [
                "replica de leitura",
                "cache distribuído",
                "jobs desacoplados por fila",
                "snapshots mensais imutáveis"
            ],
            [
                "DW analítico",
                "engines especializadas",
                "event streaming",
                "processamento near-real-time"
            ]);
    }
}
