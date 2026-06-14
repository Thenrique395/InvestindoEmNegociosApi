using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Application.Services;

internal static class DataPortabilityImportHydrator
{
    internal static UserProfile CreateUserProfile(Guid userId, UserProfileData data)
    {
        var profile = new UserProfile(
            userId,
            string.IsNullOrWhiteSpace(data.Language) ? "pt-BR" : data.Language,
            string.IsNullOrWhiteSpace(data.Currency) ? "BRL" : data.Currency,
            data.CarryOverDay,
            data.FinancialGoal ?? string.Empty,
            data.IntelligenceMode);

        profile.SetNotificationPreferences(
            data.NotifyUpcomingEnabled,
            data.NotifyOverdueEnabled,
            data.NotifyEmailEnabled,
            data.NotifyInAppEnabled,
            data.NotifyDaysBeforeDue);

        return profile;
    }

    internal static void UpdateUserProfile(UserProfile existingProfile, UserProfileData data)
    {
        existingProfile.UpdateProfileData(
            string.IsNullOrWhiteSpace(data.Language) ? "pt-BR" : data.Language,
            string.IsNullOrWhiteSpace(data.Currency) ? "BRL" : data.Currency,
            data.CarryOverDay,
            data.FinancialGoal ?? string.Empty,
            data.IntelligenceMode);

        existingProfile.SetNotificationPreferences(
            data.NotifyUpcomingEnabled,
            data.NotifyOverdueEnabled,
            data.NotifyEmailEnabled,
            data.NotifyInAppEnabled,
            data.NotifyDaysBeforeDue);
    }

    internal static MoneyPlan CreateMoneyPlan(Guid userId, MoneyPlanData data, string title, Guid? categoryId, Guid? cardId)
    {
        var plan = new MoneyPlan(
            userId,
            data.Type,
            title,
            data.Amount,
            data.Schedule,
            data.StartDate,
            data.Frequency,
            data.InstallmentsCount,
            data.DefaultPaymentMethodId,
            categoryId,
            cardId);

        plan.RestoreStatus(data.Status);
        return plan;
    }

    internal static MoneyInstallment CreateMoneyInstallment(Guid userId, Guid planId, MoneyInstallmentData data)
    {
        var installment = new MoneyInstallment(planId, userId, data.InstallmentNo, data.DueDate, data.Amount, data.OriginalDueDate);
        installment.RestoreStatus(data.Status);
        return installment;
    }

    internal static UserNotification CreateUserNotification(
        Guid userId,
        UserNotificationData data,
        string referenceKey,
        string title,
        Guid? planId,
        Guid? installmentId)
    {
        var notification = new UserNotification(
            userId,
            data.Kind,
            title,
            data.Message ?? string.Empty,
            referenceKey,
            data.MoneyType,
            planId,
            installmentId,
            data.DueDate);

        notification.RestoreReadState(data.ReadAt);
        return notification;
    }
}
