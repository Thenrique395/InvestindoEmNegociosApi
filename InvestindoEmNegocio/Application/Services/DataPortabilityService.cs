using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Application.Services;

public sealed class DataPortabilityService(
    IInvestDbContext dbContext,
    IMemoryCache cache,
    IOptions<DataPortabilityOptions> options) : IDataPortabilityService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true
    };
    private static string ExportCacheKey(Guid userId) => $"dataportability:export:{userId:N}";

    public async Task<(string FileName, byte[] Content)> ExportAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cacheSeconds = Math.Max(0, options.Value.ExportCacheSeconds);
        if (cacheSeconds > 0)
        {
            return await cache.GetOrCreateAsync(ExportCacheKey(userId), async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheSeconds);
                return await BuildExportAsync(userId, cancellationToken);
            });
        }

        return await BuildExportAsync(userId, cancellationToken);
    }

    private async Task<(string FileName, byte[] Content)> BuildExportAsync(Guid userId, CancellationToken cancellationToken)
    {
        var investmentPositions = await dbContext.InvestmentPositions.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new InvestmentPositionData(x.Id, x.Type, x.Asset, x.Quantity, x.AvgPrice, x.OpenedAt,
                x.Account, x.Category, x.Note, x.CreatedAt, x.UpdatedAt))
            .ToListAsync(cancellationToken);
        var positionIds = investmentPositions.Select(x => x.Id).ToHashSet();

        var snapshot = new UserDataSnapshot
        {
            SourceUserId = userId,
            Profile = await dbContext.UserProfiles.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new UserProfileData(
                    x.Id, x.FullName, x.Document, x.Phone, x.BirthDate, x.AvatarUrl, x.City, x.State, x.Country,
                    x.FinancialGoal, x.CarryOverDay, x.IntelligenceMode, x.Language, x.Currency, x.NotifyUpcomingEnabled, x.NotifyOverdueEnabled, x.NotifyEmailEnabled,
                    x.NotifyInAppEnabled, x.NotifyDaysBeforeDue, x.CreatedAt, x.UpdatedAt))
                .FirstOrDefaultAsync(cancellationToken),
            Categories = await dbContext.Categories.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new CategoryData(x.Id, x.Name, x.AppliesTo, x.IsActive, x.CreatedAt))
                .ToListAsync(cancellationToken),
            Cards = await dbContext.Cards.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new CardData(x.Id, x.BrandId, x.Bank, x.CreditLimit, x.StatementCloseDay, x.DueDay,
                    x.HolderName, x.Nickname, x.Last4, x.CreatedAt, x.UpdatedAt))
                .ToListAsync(cancellationToken),
            Goals = await dbContext.Goals.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new GoalData(x.Id, x.Title, x.TargetAmount, x.CurrentAmount, x.Year, x.ExpectedMonthly,
                    x.TargetDate, x.Description, x.Status, x.CreatedAt, x.UpdatedAt))
                .ToListAsync(cancellationToken),
            GoalContributions = await dbContext.GoalContributions.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new GoalContributionData(x.Id, x.GoalId, x.Amount, x.Date, x.Note, x.CreatedAt))
                .ToListAsync(cancellationToken),
            Plans = await dbContext.MoneyPlans.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new MoneyPlanData(x.Id, x.Type, x.Title, x.CategoryId, x.CardId, x.Amount, x.Schedule,
                    x.Frequency, x.InstallmentsCount, x.DefaultPaymentMethodId, x.StartDate, x.Status, x.CreatedAt, x.UpdatedAt))
                .ToListAsync(cancellationToken),
            Installments = await dbContext.MoneyInstallments.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new MoneyInstallmentData(x.Id, x.PlanId, x.InstallmentNo, x.DueDate, x.OriginalDueDate,
                    x.Amount, x.Status, x.CreatedAt, x.UpdatedAt))
                .ToListAsync(cancellationToken),
            Payments = await dbContext.MoneyPayments.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new MoneyPaymentData(x.Id, x.InstallmentId, x.PaidAt, x.PaidAmount, x.MethodId, x.Note, x.CreatedAt))
                .ToListAsync(cancellationToken),
            InvestmentGoal = await dbContext.InvestmentGoals.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new InvestmentGoalData(x.Id, x.TargetAmount, x.CreatedAt, x.UpdatedAt))
                .FirstOrDefaultAsync(cancellationToken),
            InvestmentAllocationTarget = await dbContext.InvestmentAllocationTargets.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new InvestmentAllocationTargetData(
                    x.Id,
                    x.Rf,
                    x.Acoes,
                    x.Fundos,
                    x.Cripto,
                    x.CreatedAt,
                    x.UpdatedAt))
                .FirstOrDefaultAsync(cancellationToken),
            InvestmentPositions = investmentPositions,
            InvestmentMovements = await dbContext.InvestmentMovements.AsNoTracking()
                .Where(x => positionIds.Contains(x.PositionId))
                .Select(x => new InvestmentMovementData(x.Id, x.PositionId, x.Type, x.Quantity, x.Price, x.Date, x.Note, x.CreatedAt))
                .ToListAsync(cancellationToken),
            Onboarding = await dbContext.UserOnboardings.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new UserOnboardingData(x.Id, x.Step, x.Completed, x.CreatedAt, x.UpdatedAt))
                .FirstOrDefaultAsync(cancellationToken),
            Notifications = await dbContext.UserNotifications.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new UserNotificationData(x.Id, x.PlanId, x.InstallmentId, x.MoneyType, x.Kind, x.ReferenceKey,
                    x.Title, x.Message, x.DueDate, x.CreatedAt, x.ReadAt))
                .ToListAsync(cancellationToken)
        };

        var content = JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions);
        var fileName = $"investindoemnegocios-dados-v1-{DateTime.UtcNow:yyyyMMddTHHmmssZ}.json";
        return (fileName, content);
    }

    public async Task<ImportUserDataResult> ImportAsync(Guid userId, Stream stream, bool replaceExisting, CancellationToken cancellationToken = default)
    {
        var snapshot = await JsonSerializer.DeserializeAsync<UserDataSnapshot>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Arquivo de importação inválido.");

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);

        if (replaceExisting)
        {
            await RemoveCurrentDataAsync(userId, cancellationToken);
        }

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
            var existingProfile = await dbContext.UserProfiles.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
            if (existingProfile is null)
            {
                var profile = new UserProfile(
                    userId,
                    RequireValue(p.FullName, "profile.fullName"),
                    p.Document ?? string.Empty,
                    p.Phone ?? string.Empty,
                    p.BirthDate,
                    p.AvatarUrl ?? string.Empty,
                    p.City ?? string.Empty,
                    p.State ?? string.Empty,
                    p.Country ?? string.Empty,
                    string.IsNullOrWhiteSpace(p.Language) ? "pt-BR" : p.Language,
                    string.IsNullOrWhiteSpace(p.Currency) ? "BRL" : p.Currency,
                    p.CarryOverDay,
                    p.FinancialGoal ?? string.Empty,
                    p.IntelligenceMode);
                profile.SetNotificationPreferences(p.NotifyUpcomingEnabled, p.NotifyOverdueEnabled, p.NotifyEmailEnabled, p.NotifyInAppEnabled, p.NotifyDaysBeforeDue);
                await dbContext.UserProfiles.AddAsync(profile, cancellationToken);
            }
            else
            {
                existingProfile.SetData(
                    RequireValue(p.FullName, "profile.fullName"),
                    p.Document ?? string.Empty,
                    p.Phone ?? string.Empty,
                    p.BirthDate,
                    p.AvatarUrl ?? string.Empty,
                    p.City ?? string.Empty,
                    p.State ?? string.Empty,
                    p.Country ?? string.Empty,
                    string.IsNullOrWhiteSpace(p.Language) ? "pt-BR" : p.Language,
                    string.IsNullOrWhiteSpace(p.Currency) ? "BRL" : p.Currency,
                    p.CarryOverDay,
                    p.FinancialGoal ?? string.Empty,
                    p.IntelligenceMode);
                existingProfile.SetNotificationPreferences(p.NotifyUpcomingEnabled, p.NotifyOverdueEnabled, p.NotifyEmailEnabled, p.NotifyInAppEnabled, p.NotifyDaysBeforeDue);
            }
            importedRecords++;
        }

        foreach (var c in snapshot.Categories)
        {
            var name = RequireValue(c.Name, $"categories[{c.Id}].name");
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
            var holderName = RequireValue(c.HolderName, $"cards[{c.Id}].holderName");
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
                RequireValue(c.Last4, $"cards[{c.Id}].last4"),
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
            var title = RequireValue(g.Title, $"goals[{g.Id}].title");
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
            var title = RequireValue(p.Title, $"plans[{p.Id}].title");
            var categoryId = p.CategoryId.HasValue && categoryMap.TryGetValue(p.CategoryId.Value, out var newCategoryId)
                ? newCategoryId
                : p.CategoryId;
            var cardId = p.CardId.HasValue && cardMap.TryGetValue(p.CardId.Value, out var newCardId)
                ? newCardId
                : p.CardId;

            var plan = new MoneyPlan(
                userId,
                p.Type,
                title,
                p.Amount,
                p.Schedule,
                p.StartDate,
                p.Frequency,
                p.InstallmentsCount,
                p.DefaultPaymentMethodId,
                categoryId,
                cardId);
            SetValue(plan, nameof(MoneyPlan.Status), p.Status);
            await dbContext.MoneyPlans.AddAsync(plan, cancellationToken);
            planMap[p.Id] = plan.Id;
            importedRecords++;
        }

        foreach (var i in snapshot.Installments)
        {
            if (!planMap.TryGetValue(i.PlanId, out var mappedPlanId))
            {
                continue;
            }

            var installment = new MoneyInstallment(mappedPlanId, userId, i.InstallmentNo, i.DueDate, i.Amount, i.OriginalDueDate);
            SetValue(installment, nameof(MoneyInstallment.Status), i.Status);
            await dbContext.MoneyInstallments.AddAsync(installment, cancellationToken);
            installmentMap[i.Id] = installment.Id;
            importedRecords++;
        }

        foreach (var p in snapshot.Payments)
        {
            if (!installmentMap.TryGetValue(p.InstallmentId, out var mappedInstallmentId))
            {
                continue;
            }

            var payment = new MoneyPayment(mappedInstallmentId, userId, p.PaidAt, p.PaidAmount, p.MethodId, p.Note);
            await dbContext.MoneyPayments.AddAsync(payment, cancellationToken);
            importedRecords++;
        }

        foreach (var c in snapshot.GoalContributions)
        {
            if (!goalMap.TryGetValue(c.GoalId, out var mappedGoalId))
            {
                continue;
            }

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
                RequireValue(p.Asset, $"investmentPositions[{p.Id}].asset"),
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
            {
                continue;
            }

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
            var normalizedReference = RequireValue(n.ReferenceKey, $"notifications[{n.Id}].referenceKey");
            if (existingNotificationReferences.Contains(normalizedReference))
            {
                continue;
            }

            var mappedPlanId = n.PlanId.HasValue && planMap.TryGetValue(n.PlanId.Value, out var pId) ? pId : (Guid?)null;
            var mappedInstallmentId = n.InstallmentId.HasValue && installmentMap.TryGetValue(n.InstallmentId.Value, out var iId) ? iId : (Guid?)null;
            var notification = new UserNotification(
                userId,
                n.Kind,
                RequireValue(n.Title, $"notifications[{n.Id}].title"),
                n.Message ?? string.Empty,
                normalizedReference,
                n.MoneyType,
                mappedPlanId,
                mappedInstallmentId,
                n.DueDate);
            SetValue(notification, nameof(UserNotification.ReadAt), n.ReadAt);
            await dbContext.UserNotifications.AddAsync(notification, cancellationToken);
            existingNotificationReferences.Add(normalizedReference);
            importedRecords++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        cache.Remove(ExportCacheKey(userId));
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

    private static string RequireValue(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Arquivo inválido: campo obrigatório ausente ({field}).");
        }

        return value.Trim();
    }

    private static void SetValue<TEntity>(TEntity entity, string propertyName, object? value)
    {
        var property = typeof(TEntity).GetProperty(propertyName);
        property?.SetValue(entity, value);
    }
}
