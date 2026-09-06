using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Application.Services;

/// <summary>
/// Lê os valores efetivado (Realized) e pendente (Pending) de uma meta em um
/// período, a partir dos lançamentos reais. Fonte única compartilhada por
/// progresso e ocorrências.
///
/// Efetivado = parcelas Paid/Anticipated. Pendente = Open/PartiallyPaid.
/// Canceladas e transferências nunca entram. Escopo por categoria (vazio = todas
/// do tipo). Investimento = aportes (GoalContribution).
/// </summary>
public sealed class GoalRealizedReader(IInvestDbContext db) : IGoalRealizedReader
{
    /* Antecipada NÃO é efetivada: antecipar só muda o vencimento de mês, sem
       registrar pagamento (MoneyInstallment.Anticipate), e a parcela continua
       podendo ser paga depois. Ela conta como pendente, junto com as abertas. */
    private static readonly InstallmentStatus[] Effected = [InstallmentStatus.Paid];
    private static readonly InstallmentStatus[] PendingStatuses =
        [InstallmentStatus.Open, InstallmentStatus.PartiallyPaid, InstallmentStatus.Anticipated];

    public async Task<(decimal Realized, decimal Pending)> ReadAsync(Goal goal, DateOnly start, DateOnly end, CancellationToken ct = default)
    {
        if (goal.Kind == GoalKind.Investment)
        {
            var contributions = await db.GoalContributions
                .Where(c => c.GoalId == goal.Id && c.UserId == goal.UserId && c.Date >= start && c.Date <= end)
                .Select(c => c.Amount)
                .ToListAsync(ct);
            return (contributions.Sum(), 0m);
        }

        var moneyType = goal.Kind == GoalKind.Income ? MoneyType.Income : MoneyType.Expense;
        var categoryIds = goal.Scopes
            .Where(s => s.ScopeType == GoalScopeType.Category)
            .Select(s => s.RefId)
            .ToList();

        var query = db.MoneyInstallments
            .Join(db.MoneyPlans, i => i.PlanId, p => p.Id, (i, p) => new { i, p })
            .Where(x => x.i.UserId == goal.UserId
                        && x.i.SpaceId == goal.SpaceId
                        && x.p.Type == moneyType
                        && x.i.DueDate >= start && x.i.DueDate <= end);

        if (categoryIds.Count > 0)
            query = query.Where(x => x.p.CategoryId != null && categoryIds.Contains(x.p.CategoryId.Value));

        // Soma no cliente: SQLite (testes) não agrega decimal no servidor.
        var rows = await query.Select(x => new { x.i.Amount, x.i.Status }).ToListAsync(ct);
        var realized = rows.Where(r => Effected.Contains(r.Status)).Sum(r => r.Amount);
        var pending = rows.Where(r => PendingStatuses.Contains(r.Status)).Sum(r => r.Amount);
        return (realized, pending);
    }
}
