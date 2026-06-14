using System.Text.Json;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Application.Services;

public sealed class DataPortabilityExportService(
    IInvestDbContext dbContext,
    IMemoryCache cache,
    IOptions<DataPortabilityOptions> options) : IDataPortabilityExportService
{
    public async Task<(string FileName, byte[] Content)> ExportAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cacheSeconds = Math.Max(0, options.Value.ExportCacheSeconds);
        if (cacheSeconds > 0)
        {
            var cachedExport = await cache.GetOrCreateAsync(DataPortabilityShared.ExportCacheKey(userId), async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheSeconds);
                return await BuildExportAsync(userId, cancellationToken);
            });

            return cachedExport;
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

        var userData = await dbContext.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new { x.Name, x.Document, x.Phone, x.BirthDate, x.AvatarUrl, x.City, x.State, x.Country })
            .FirstOrDefaultAsync(cancellationToken);
        var userFullName = userData?.Name ?? string.Empty;
        var userDocument = userData?.Document ?? string.Empty;
        var userPhone = userData?.Phone ?? string.Empty;
        var userBirthDate = userData?.BirthDate;
        var userAvatarUrl = userData?.AvatarUrl ?? string.Empty;
        var userCity = userData?.City ?? string.Empty;
        var userState = userData?.State ?? string.Empty;
        var userCountry = userData?.Country ?? string.Empty;

        var snapshot = new UserDataSnapshot
        {
            SourceUserId = userId,
            Profile = await dbContext.UserProfiles.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new UserProfileData(
                    x.Id, userFullName, userDocument, userPhone, userBirthDate, userAvatarUrl, userCity, userState, userCountry,
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

        var content = JsonSerializer.SerializeToUtf8Bytes(snapshot, DataPortabilityShared.JsonOptions);
        var fileName = $"investindoemnegocios-dados-v1-{DateTime.UtcNow:yyyyMMddTHHmmssZ}.json";
        return (fileName, content);
    }
}
