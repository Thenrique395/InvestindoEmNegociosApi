using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IInvestmentsService :
    IInvestmentGoalQueryService,
    IInvestmentGoalCommandService,
    IInvestmentAllocationQueryService,
    IInvestmentAllocationCommandService,
    IInvestmentPositionQueryService,
    IInvestmentPositionCommandService,
    IInvestmentMarketEnrichmentService
{
}
