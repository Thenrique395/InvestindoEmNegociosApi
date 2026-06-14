using System.Text.Json;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace InvestindoEmNegocio.Application.Services;

public sealed class DataPortabilityImportService(
    IInvestDbContext dbContext,
    IMemoryCache cache) : IDataPortabilityImportService
{
    public async Task<ImportUserDataResult> ImportAsync(Guid userId, Stream stream, bool replaceExisting, CancellationToken cancellationToken = default)
    {
        var snapshot = await JsonSerializer.DeserializeAsync<UserDataSnapshot>(stream, DataPortabilityShared.JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Arquivo de importação inválido.");

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);

        if (replaceExisting)
            await RemoveCurrentDataAsync(userId, cancellationToken);

        var importedRecords = 0;
        var categoryMap = new Dictionary<Guid, Guid>();
        var cardMap = new Dictionary<Guid, Guid>();
        var goalMap = new Dictionary<Guid, Guid>();
        var planMap = new Dictionary<Guid, Guid>();
        var installmentMap = new Dictionary<Guid, Guid>();
        var positionMap = new Dictionary<Guid, Guid>();
        var existingCategoriesByName = await dbContext.Categories
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToDictionaryAsync(x => x.Name.Trim().ToLowerInvariant(), x => x.Id, cancellationToken);
        var existingCardsByNickname = await dbContext.Cards
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToDictionaryAsync(x => x.Nickname.Trim().ToLowerInvariant(), x => x.Id, cancellationToken);
        var existingGoalsByYearTitle = await dbContext.Goals
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToDictionaryAsync(x => $"{x.Year}:{x.Title.Trim().ToLowerInvariant()}", x => x.Id, cancellationToken);

        if (snapshot.Profile is not null)
        {
            var p = snapshot.Profile;
            var fullName = DataPortabilityShared.RequireTrimmedValue(p.FullName, "profile.fullName");
            var existingProfile = await dbContext.UserProfiles.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
            if (existingProfile is null)
            {
                var profile = DataPortabilityImportHydrator.CreateUserProfile(userId, p);
                await dbContext.UserProfiles.AddAsync(profile, cancellationToken);
            }
            else
            {
                DataPortabilityImportHydrator.UpdateUserProfile(existingProfile, p);
            }

            var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
            if (user is not null)
            {
                if (!string.IsNullOrWhiteSpace(p.Document) && string.IsNullOrEmpty(user.Document))
                    user.SetDocument(p.Document);

                user.UpdateProfileDetails(p.AvatarUrl ?? string.Empty, p.City ?? string.Empty, p.State ?? string.Empty, p.Country ?? string.Empty);
                user.UpdateIdentityDetails(fullName, p.Phone ?? string.Empty, p.BirthDate);
            }

            importedRecords++;
        }

        foreach (var c in snapshot.Categories)
        {
            var name = DataPortabilityShared.RequireTrimmedValue(c.Name, $"categories[{c.Id}].name");
            var categoryKey = name.ToLowerInvariant();
            if (existingCategoriesByName.TryGetValue(categoryKey, out var existingCategoryId))
            {
                categoryMap[c.Id] = existingCategoryId;
                continue;
            }

            var category = new Category(userId, name, c.AppliesTo);
            if (!c.IsActive) category.Deactivate();
            await dbContext.Categories.AddAsync(category, cancellationToken);
            categoryMap[c.Id] = category.Id;
            existingCategoriesByName[categoryKey] = category.Id;
            importedRecords++;
        }

        foreach (var c in snapshot.Cards)
        {
            var holderName = DataPortabilityShared.RequireTrimmedValue(c.HolderName, $"cards[{c.Id}].holderName");
            var nickname = string.IsNullOrWhiteSpace(c.Nickname) ? holderName : c.Nickname.Trim();
            var nicknameKey = nickname.ToLowerInvariant();
            if (existingCardsByNickname.TryGetValue(nicknameKey, out var existingCardId))
            {
                cardMap[c.Id] = existingCardId;
                continue;
            }

            var card = new Card(
                userId,
                c.BrandId,
                holderName,
                nickname,
                DataPortabilityShared.RequireTrimmedValue(c.Last4, $"cards[{c.Id}].last4"),
                c.Bank,
                c.CreditLimit,
                c.StatementCloseDay,
                c.DueDay);
            await dbContext.Cards.AddAsync(card, cancellationToken);
            cardMap[c.Id] = card.Id;
            existingCardsByNickname[nicknameKey] = card.Id;
            importedRecords++;
        }

        foreach (var g in snapshot.Goals)
        {
            var title = DataPortabilityShared.RequireTrimmedValue(g.Title, $"goals[{g.Id}].title");
            var goalKey = $"{g.Year}:{title.ToLowerInvariant()}";
            if (existingGoalsByYearTitle.TryGetValue(goalKey, out var existingGoalId))
            {
                goalMap[g.Id] = existingGoalId;
                continue;
            }

            var goal = new Goal(
                userId,
                title,
                g.TargetAmount,
                g.Year,
                g.Description,
                g.Status,
                g.CurrentAmount,
                g.ExpectedMonthly,
                g.TargetDate);
            await dbContext.Goals.AddAsync(goal, cancellationToken);
            goalMap[g.Id] = goal.Id;
            existingGoalsByYearTitle[goalKey] = goal.Id;
            importedRecords++;
        }

        foreach (var p in snapshot.Plans)
        {
            var title = DataPortabilityShared.RequireTrimmedValue(p.Title, $"plans[{p.Id}].title");
            var categoryId = p.CategoryId.HasValue && categoryMap.TryGetValue(p.CategoryId.Value, out var newCategoryId)
                ? newCategoryId
                : p.CategoryId;
            var cardId = p.CardId.HasValue && cardMap.TryGetValue(p.CardId.Value, out var newCardId)
                ? newCardId
                : p.CardId;

            var plan = DataPortabilityImportHydrator.CreateMoneyPlan(userId, p, title, categoryId, cardId);
            await dbContext.MoneyPlans.AddAsync(plan, cancellationToken);
            planMap[p.Id] = plan.Id;
            importedRecords++;
        }

        foreach (var i in snapshot.Installments)
        {
            if (!planMap.TryGetValue(i.PlanId, out var mappedPlanId))
                continue;

            var installment = DataPortabilityImportHydrator.CreateMoneyInstallment(userId, mappedPlanId, i);
            await dbContext.MoneyInstallments.AddAsync(installment, cancellationToken);
            installmentMap[i.Id] = installment.Id;
            importedRecords++;
        }

        foreach (var p in snapshot.Payments)
        {
            if (!installmentMap.TryGetValue(p.InstallmentId, out var mappedInstallmentId))
                continue;

            var payment = new MoneyPayment(mappedInstallmentId, userId, p.PaidAt, p.PaidAmount, p.MethodId, p.Note);
            await dbContext.MoneyPayments.AddAsync(payment, cancellationToken);
            importedRecords++;
        }

        foreach (var c in snapshot.GoalContributions)
        {
            if (!goalMap.TryGetValue(c.GoalId, out var mappedGoalId))
                continue;

            var contribution = new GoalContribution(mappedGoalId, userId, c.Amount, c.Date, c.Note);
            await dbContext.GoalContributions.AddAsync(contribution, cancellationToken);
            importedRecords++;
        }

        if (snapshot.InvestmentGoal is not null)
        {
            var g = snapshot.InvestmentGoal;
            var existingInvestmentGoal = await dbContext.InvestmentGoals.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
            if (existingInvestmentGoal is null)
            {
                var goal = new InvestmentGoal(userId, g.TargetAmount);
                await dbContext.InvestmentGoals.AddAsync(goal, cancellationToken);
                importedRecords++;
            }
            else
            {
                existingInvestmentGoal.SetTargetAmount(g.TargetAmount);
            }
        }

        if (snapshot.InvestmentAllocationTarget is not null)
        {
            var t = snapshot.InvestmentAllocationTarget;
            var existingTarget = await dbContext.InvestmentAllocationTargets.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
            if (existingTarget is null)
            {
                var target = new InvestmentAllocationTarget(userId, t.Rf, t.Acoes, t.Fundos, t.Cripto);
                await dbContext.InvestmentAllocationTargets.AddAsync(target, cancellationToken);
                importedRecords++;
            }
            else
            {
                existingTarget.SetAllocation(t.Rf, t.Acoes, t.Fundos, t.Cripto);
            }
        }

        foreach (var p in snapshot.InvestmentPositions)
        {
            var position = new InvestmentPosition(
                userId,
                p.Type,
                DataPortabilityShared.RequireTrimmedValue(p.Asset, $"investmentPositions[{p.Id}].asset"),
                p.Quantity,
                p.AvgPrice,
                p.OpenedAt,
                p.Account ?? string.Empty,
                p.Category ?? string.Empty,
                p.Note);
            await dbContext.InvestmentPositions.AddAsync(position, cancellationToken);
            positionMap[p.Id] = position.Id;
            importedRecords++;
        }

        foreach (var m in snapshot.InvestmentMovements)
        {
            if (!positionMap.TryGetValue(m.PositionId, out var mappedPositionId))
                continue;

            var movement = new InvestmentMovement(mappedPositionId, m.Type, m.Quantity, m.Price, m.Date, m.Note);
            await dbContext.InvestmentMovements.AddAsync(movement, cancellationToken);
            importedRecords++;
        }

        if (snapshot.Onboarding is not null)
        {
            var o = snapshot.Onboarding;
            var existingOnboarding = await dbContext.UserOnboardings.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
            if (existingOnboarding is null)
            {
                var onboarding = new UserOnboarding(userId, o.Step, o.Completed);
                await dbContext.UserOnboardings.AddAsync(onboarding, cancellationToken);
                importedRecords++;
            }
            else
            {
                existingOnboarding.Update(o.Step, o.Completed);
            }
        }

        var existingNotificationReferences = (await dbContext.UserNotifications
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.ReferenceKey)
            .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var n in snapshot.Notifications)
        {
            var normalizedReference = DataPortabilityShared.RequireTrimmedValue(n.ReferenceKey, $"notifications[{n.Id}].referenceKey");
            if (existingNotificationReferences.Contains(normalizedReference))
                continue;

            var mappedPlanId = n.PlanId.HasValue && planMap.TryGetValue(n.PlanId.Value, out var pId) ? pId : (Guid?)null;
            var mappedInstallmentId = n.InstallmentId.HasValue && installmentMap.TryGetValue(n.InstallmentId.Value, out var iId) ? iId : (Guid?)null;
            var title = DataPortabilityShared.RequireTrimmedValue(n.Title, $"notifications[{n.Id}].title");
            var notification = DataPortabilityImportHydrator.CreateUserNotification(
                userId,
                n,
                normalizedReference,
                title,
                mappedPlanId,
                mappedInstallmentId);
            await dbContext.UserNotifications.AddAsync(notification, cancellationToken);
            existingNotificationReferences.Add(normalizedReference);
            importedRecords++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        cache.Remove(DataPortabilityShared.ExportCacheKey(userId));
        return new ImportUserDataResult(importedRecords);
    }

    private async Task RemoveCurrentDataAsync(Guid userId, CancellationToken cancellationToken)
    {
        var positionIds = await dbContext.InvestmentPositions
            .Where(x => x.UserId == userId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        dbContext.MoneyPayments.RemoveRange(dbContext.MoneyPayments.Where(x => x.UserId == userId));
        dbContext.MoneyInstallments.RemoveRange(dbContext.MoneyInstallments.Where(x => x.UserId == userId));
        dbContext.GoalContributions.RemoveRange(dbContext.GoalContributions.Where(x => x.UserId == userId));
        dbContext.InvestmentMovements.RemoveRange(dbContext.InvestmentMovements.Where(x => positionIds.Contains(x.PositionId)));
        dbContext.UserNotifications.RemoveRange(dbContext.UserNotifications.Where(x => x.UserId == userId));
        dbContext.InvestmentPositions.RemoveRange(dbContext.InvestmentPositions.Where(x => x.UserId == userId));
        dbContext.InvestmentGoals.RemoveRange(dbContext.InvestmentGoals.Where(x => x.UserId == userId));
        dbContext.InvestmentAllocationTargets.RemoveRange(dbContext.InvestmentAllocationTargets.Where(x => x.UserId == userId));
        dbContext.MoneyPlans.RemoveRange(dbContext.MoneyPlans.Where(x => x.UserId == userId));
        dbContext.Goals.RemoveRange(dbContext.Goals.Where(x => x.UserId == userId));
        dbContext.Cards.RemoveRange(dbContext.Cards.Where(x => x.UserId == userId));
        dbContext.Categories.RemoveRange(dbContext.Categories.Where(x => x.UserId == userId));
        dbContext.UserOnboardings.RemoveRange(dbContext.UserOnboardings.Where(x => x.UserId == userId));
        dbContext.UserProfiles.RemoveRange(dbContext.UserProfiles.Where(x => x.UserId == userId));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
