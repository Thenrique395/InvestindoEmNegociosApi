using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Infrastructure.Data;

namespace InvestindoEmNegocio.Application.Services;

public sealed class DataPortabilityService(InvestDbContext dbContext) : IDataPortabilityService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true
    };

    public async Task<(string FileName, byte[] Content)> ExportAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var positionIds = await dbContext.InvestmentPositions
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var snapshot = new UserDataSnapshot
        {
            SourceUserId = userId,
            Profile = await dbContext.UserProfiles.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new UserProfileData(
                    x.Id, x.FullName, x.Document, x.Phone, x.BirthDate, x.AvatarUrl, x.City, x.State, x.Country,
                    x.Language, x.Currency, x.NotifyUpcomingEnabled, x.NotifyOverdueEnabled, x.NotifyEmailEnabled,
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
            InvestmentPositions = await dbContext.InvestmentPositions.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new InvestmentPositionData(x.Id, x.Type, x.Asset, x.Quantity, x.AvgPrice, x.OpenedAt,
                    x.Account, x.Category, x.Note, x.CreatedAt, x.UpdatedAt))
                .ToListAsync(cancellationToken),
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
        var fileName = $"investindo-user-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
        return (fileName, content);
    }

    public async Task<ImportUserDataResult> ImportAsync(Guid userId, Stream stream, bool replaceExisting, CancellationToken cancellationToken = default)
    {
        var snapshot = await JsonSerializer.DeserializeAsync<UserDataSnapshot>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Arquivo de importação inválido.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (replaceExisting)
        {
            await RemoveCurrentDataAsync(userId, cancellationToken);
        }

        var importedRecords = 0;

        if (snapshot.Profile is not null)
        {
            var p = snapshot.Profile;
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
                string.IsNullOrWhiteSpace(p.Currency) ? "BRL" : p.Currency);
            profile.SetNotificationPreferences(p.NotifyUpcomingEnabled, p.NotifyOverdueEnabled, p.NotifyEmailEnabled, p.NotifyInAppEnabled, p.NotifyDaysBeforeDue);
            Set(profile, nameof(UserProfile.Id), p.Id);
            Set(profile, nameof(UserProfile.CreatedAt), p.CreatedAt);
            Set(profile, nameof(UserProfile.UpdatedAt), p.UpdatedAt);
            await dbContext.UserProfiles.AddAsync(profile, cancellationToken);
            importedRecords++;
        }

        foreach (var c in snapshot.Categories)
        {
            var category = new Category(userId, RequireValue(c.Name, $"categories[{c.Id}].name"), c.AppliesTo);
            if (!c.IsActive) category.Deactivate();
            Set(category, nameof(Category.Id), c.Id);
            Set(category, nameof(Category.CreatedAt), c.CreatedAt);
            await dbContext.Categories.AddAsync(category, cancellationToken);
            importedRecords++;
        }

        foreach (var c in snapshot.Cards)
        {
            var card = new Card(
                userId,
                c.BrandId,
                RequireValue(c.HolderName, $"cards[{c.Id}].holderName"),
                string.IsNullOrWhiteSpace(c.Nickname) ? c.HolderName : c.Nickname,
                RequireValue(c.Last4, $"cards[{c.Id}].last4"),
                c.Bank,
                c.CreditLimit,
                c.StatementCloseDay,
                c.DueDay);
            Set(card, nameof(Card.Id), c.Id);
            Set(card, nameof(Card.CreatedAt), c.CreatedAt);
            Set(card, nameof(Card.UpdatedAt), c.UpdatedAt);
            await dbContext.Cards.AddAsync(card, cancellationToken);
            importedRecords++;
        }

        foreach (var g in snapshot.Goals)
        {
            var goal = new Goal(
                userId,
                RequireValue(g.Title, $"goals[{g.Id}].title"),
                g.TargetAmount,
                g.Year,
                g.Description,
                g.Status,
                g.CurrentAmount,
                g.ExpectedMonthly,
                g.TargetDate);
            Set(goal, nameof(Goal.Id), g.Id);
            Set(goal, nameof(Goal.CreatedAt), g.CreatedAt);
            Set(goal, nameof(Goal.UpdatedAt), g.UpdatedAt);
            await dbContext.Goals.AddAsync(goal, cancellationToken);
            importedRecords++;
        }

        foreach (var p in snapshot.Plans)
        {
            var plan = new MoneyPlan(
                userId,
                p.Type,
                RequireValue(p.Title, $"plans[{p.Id}].title"),
                p.Amount,
                p.Schedule,
                p.StartDate,
                p.Frequency,
                p.InstallmentsCount,
                p.DefaultPaymentMethodId,
                p.CategoryId,
                p.CardId);
            Set(plan, nameof(MoneyPlan.Id), p.Id);
            Set(plan, nameof(MoneyPlan.Status), p.Status);
            Set(plan, nameof(MoneyPlan.CreatedAt), p.CreatedAt);
            Set(plan, nameof(MoneyPlan.UpdatedAt), p.UpdatedAt);
            await dbContext.MoneyPlans.AddAsync(plan, cancellationToken);
            importedRecords++;
        }

        foreach (var i in snapshot.Installments)
        {
            var installment = new MoneyInstallment(i.PlanId, userId, i.InstallmentNo, i.DueDate, i.Amount, i.OriginalDueDate);
            Set(installment, nameof(MoneyInstallment.Id), i.Id);
            Set(installment, nameof(MoneyInstallment.Status), i.Status);
            Set(installment, nameof(MoneyInstallment.CreatedAt), i.CreatedAt);
            Set(installment, nameof(MoneyInstallment.UpdatedAt), i.UpdatedAt);
            await dbContext.MoneyInstallments.AddAsync(installment, cancellationToken);
            importedRecords++;
        }

        foreach (var p in snapshot.Payments)
        {
            var payment = new MoneyPayment(p.InstallmentId, userId, p.PaidAt, p.PaidAmount, p.MethodId, p.Note);
            Set(payment, nameof(MoneyPayment.Id), p.Id);
            Set(payment, nameof(MoneyPayment.CreatedAt), p.CreatedAt);
            await dbContext.MoneyPayments.AddAsync(payment, cancellationToken);
            importedRecords++;
        }

        foreach (var c in snapshot.GoalContributions)
        {
            var contribution = new GoalContribution(c.GoalId, userId, c.Amount, c.Date, c.Note);
            Set(contribution, nameof(GoalContribution.Id), c.Id);
            Set(contribution, nameof(GoalContribution.CreatedAt), c.CreatedAt);
            await dbContext.GoalContributions.AddAsync(contribution, cancellationToken);
            importedRecords++;
        }

        if (snapshot.InvestmentGoal is not null)
        {
            var g = snapshot.InvestmentGoal;
            var goal = new InvestmentGoal(userId, g.TargetAmount);
            Set(goal, nameof(InvestmentGoal.Id), g.Id);
            Set(goal, nameof(InvestmentGoal.CreatedAt), g.CreatedAt);
            Set(goal, nameof(InvestmentGoal.UpdatedAt), g.UpdatedAt);
            await dbContext.InvestmentGoals.AddAsync(goal, cancellationToken);
            importedRecords++;
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
            Set(position, nameof(InvestmentPosition.Id), p.Id);
            Set(position, nameof(InvestmentPosition.CreatedAt), p.CreatedAt);
            Set(position, nameof(InvestmentPosition.UpdatedAt), p.UpdatedAt);
            await dbContext.InvestmentPositions.AddAsync(position, cancellationToken);
            importedRecords++;
        }

        foreach (var m in snapshot.InvestmentMovements)
        {
            var movement = new InvestmentMovement(m.PositionId, m.Type, m.Quantity, m.Price, m.Date, m.Note);
            Set(movement, nameof(InvestmentMovement.Id), m.Id);
            Set(movement, nameof(InvestmentMovement.CreatedAt), m.CreatedAt);
            await dbContext.InvestmentMovements.AddAsync(movement, cancellationToken);
            importedRecords++;
        }

        if (snapshot.Onboarding is not null)
        {
            var o = snapshot.Onboarding;
            var onboarding = new UserOnboarding(userId, o.Step, o.Completed);
            Set(onboarding, nameof(UserOnboarding.Id), o.Id);
            Set(onboarding, nameof(UserOnboarding.CreatedAt), o.CreatedAt);
            Set(onboarding, nameof(UserOnboarding.UpdatedAt), o.UpdatedAt);
            await dbContext.UserOnboardings.AddAsync(onboarding, cancellationToken);
            importedRecords++;
        }

        foreach (var n in snapshot.Notifications)
        {
            var notification = new UserNotification(
                userId,
                n.Kind,
                RequireValue(n.Title, $"notifications[{n.Id}].title"),
                n.Message ?? string.Empty,
                RequireValue(n.ReferenceKey, $"notifications[{n.Id}].referenceKey"),
                n.MoneyType,
                n.PlanId,
                n.InstallmentId,
                n.DueDate);
            Set(notification, nameof(UserNotification.Id), n.Id);
            Set(notification, nameof(UserNotification.CreatedAt), n.CreatedAt);
            Set(notification, nameof(UserNotification.ReadAt), n.ReadAt);
            await dbContext.UserNotifications.AddAsync(notification, cancellationToken);
            importedRecords++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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
        dbContext.MoneyPlans.RemoveRange(dbContext.MoneyPlans.Where(x => x.UserId == userId));
        dbContext.Goals.RemoveRange(dbContext.Goals.Where(x => x.UserId == userId));
        dbContext.Cards.RemoveRange(dbContext.Cards.Where(x => x.UserId == userId));
        dbContext.Categories.RemoveRange(dbContext.Categories.Where(x => x.UserId == userId));
        dbContext.UserOnboardings.RemoveRange(dbContext.UserOnboardings.Where(x => x.UserId == userId));
        dbContext.UserProfiles.RemoveRange(dbContext.UserProfiles.Where(x => x.UserId == userId));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void Set<TEntity>(TEntity entity, string propertyName, object? value)
    {
        var property = typeof(TEntity).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property is null || !property.CanWrite) return;
        property.SetValue(entity, value);
    }

    private static string RequireValue(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Arquivo inválido: campo obrigatório ausente ({field}).");
        }

        return value.Trim();
    }
}
