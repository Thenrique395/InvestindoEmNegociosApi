using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public class BudgetService(
    IMonthlyBudgetRepository budgetRepository,
    IMoneyInstallmentRepository installmentRepository,
    IMoneyPlanRepository moneyPlanRepository,
    ICategoryRepository categoryRepository) : IBudgetService
{
    public async Task<BudgetResponse> GetAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default)
    {
        var budgetItems = await budgetRepository.ListByMonthAsync(userId, year, month, cancellationToken);
        var realized = await GetRealizedByCategory(userId, year, month, cancellationToken);

        var items = budgetItems
            .Select(b =>
            {
                realized.TryGetValue(b.CategoryName, out var realizedAmount);
                return new BudgetItemResponse(b.Id, b.CategoryName, b.PlannedAmount, realizedAmount, b.PlannedAmount - realizedAmount);
            })
            .ToList();

        return BuildResponse(year, month, items);
    }

    public async Task<BudgetResponse> UpsertItemsAsync(Guid userId, int year, int month, IReadOnlyList<BudgetItemRequest> items, CancellationToken cancellationToken = default)
    {
        var existing = await budgetRepository.ListByMonthAsync(userId, year, month, cancellationToken);
        var existingByCategory = existing.ToDictionary(x => x.CategoryName, StringComparer.OrdinalIgnoreCase);

        foreach (var req in items)
        {
            var name = req.CategoryName.Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;

            if (existingByCategory.TryGetValue(name, out var found))
            {
                found.Update(req.PlannedAmount);
            }
            else
            {
                var newItem = new MonthlyBudgetItem(userId, year, month, name, req.PlannedAmount);
                await budgetRepository.AddAsync(newItem, cancellationToken);
            }
        }

        await budgetRepository.SaveChangesAsync(cancellationToken);

        return await GetAsync(userId, year, month, cancellationToken);
    }

    public async Task DeleteItemAsync(Guid userId, Guid itemId, CancellationToken cancellationToken = default)
    {
        var item = await budgetRepository.GetByIdAsync(itemId, userId, cancellationToken)
            ?? throw new AppProblemException("Item não encontrado", "O item de orçamento não existe ou não pertence ao usuário.", StatusCodes.Status404NotFound);

        budgetRepository.Remove(item);
        await budgetRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task<Dictionary<string, decimal>> GetRealizedByCategory(Guid userId, int year, int month, CancellationToken cancellationToken)
    {
        var periodStart = new DateOnly(year, month, 1);
        var periodEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

        var installments = await installmentRepository.ListByUserAsync(
            userId, InstallmentStatus.Paid, periodStart, periodEnd, MoneyType.Expense, cancellationToken);

        var plans = await moneyPlanRepository.ListByUserAsync(userId, MoneyType.Expense, cancellationToken);
        var planMap = plans.ToDictionary(p => p.Id);

        var categories = await categoryRepository.ListForUserAsync(userId, MoneyType.Expense, cancellationToken);
        var categoryMap = categories.ToDictionary(c => c.Id, c => c.Name);

        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var inst in installments)
        {
            if (!planMap.TryGetValue(inst.PlanId, out var plan)) continue;

            var categoryName = plan.CategoryId.HasValue && categoryMap.TryGetValue(plan.CategoryId.Value, out var catName)
                ? catName
                : "Sem categoria";

            result[categoryName] = result.GetValueOrDefault(categoryName) + inst.Amount;
        }

        return result;
    }

    private static BudgetResponse BuildResponse(int year, int month, List<BudgetItemResponse> items)
    {
        var totalPlanned = items.Sum(x => x.PlannedAmount);
        var totalRealized = items.Sum(x => x.RealizedAmount);
        return new BudgetResponse(year, month, totalPlanned, totalRealized, totalPlanned - totalRealized, items);
    }
}
