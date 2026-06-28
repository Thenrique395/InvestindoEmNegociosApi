using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Security;

public static class AppFeatureMatrix
{
    private static readonly IReadOnlyDictionary<UserRole, HashSet<string>> Matrix =
        new Dictionary<UserRole, HashSet<string>>
        {
            [UserRole.Basic] = new(StringComparer.OrdinalIgnoreCase)
            {
                AppFeatureKeys.AuthSecurityManage,
                AppFeatureKeys.BillingManage,
                AppFeatureKeys.SubscriptionsManage,
                AppFeatureKeys.ProfileManage,
                AppFeatureKeys.PreferencesManage,
                AppFeatureKeys.SpacesManage,
                AppFeatureKeys.OnboardingManage,
                AppFeatureKeys.NotificationsAccess,
                AppFeatureKeys.LookupsRead,
                AppFeatureKeys.DataPortabilityExport,
                AppFeatureKeys.PlansManage,
                AppFeatureKeys.IncomesRead,
                AppFeatureKeys.GoalsManage,
                AppFeatureKeys.GoalContributionsManage,
                AppFeatureKeys.InstallmentsRead,
                AppFeatureKeys.InstallmentsPay,
                AppFeatureKeys.InstallmentsManage,
                AppFeatureKeys.AccountsRead,
                AppFeatureKeys.CardsRead,
                AppFeatureKeys.CardsCreateUpdate,
                AppFeatureKeys.CardsDelete,
                AppFeatureKeys.CategoriesRead
            },
            [UserRole.Intermediate] = new(StringComparer.OrdinalIgnoreCase)
            {
                AppFeatureKeys.AuthSecurityManage,
                AppFeatureKeys.BillingManage,
                AppFeatureKeys.SubscriptionsManage,
                AppFeatureKeys.ProfileManage,
                AppFeatureKeys.PreferencesManage,
                AppFeatureKeys.SpacesManage,
                AppFeatureKeys.OnboardingManage,
                AppFeatureKeys.NotificationsAccess,
                AppFeatureKeys.LookupsRead,
                AppFeatureKeys.DataPortabilityExport,
                AppFeatureKeys.DataPortabilityImport,
                AppFeatureKeys.PlansManage,
                AppFeatureKeys.IncomesRead,
                AppFeatureKeys.IncomesSummaryRead,
                AppFeatureKeys.GoalsManage,
                AppFeatureKeys.GoalContributionsManage,
                AppFeatureKeys.InstallmentsRead,
                AppFeatureKeys.InstallmentsPay,
                AppFeatureKeys.InstallmentsManage,
                AppFeatureKeys.InstallmentsAnticipate,
                AppFeatureKeys.AccountsRead,
                AppFeatureKeys.AccountsManage,
                AppFeatureKeys.AccountsAnalytics,
                AppFeatureKeys.AccountsImport,
                AppFeatureKeys.CardsRead,
                AppFeatureKeys.CardsCreateUpdate,
                AppFeatureKeys.CardsDelete,
                AppFeatureKeys.CardsStatementsRead,
                AppFeatureKeys.CategoriesRead,
                AppFeatureKeys.CategoriesManage,
                AppFeatureKeys.InvoiceImportAccess,
                AppFeatureKeys.FinancialAssistantAccess,
                AppFeatureKeys.MonthlySnapshotsAccess,
                AppFeatureKeys.LoansAccess,
                AppFeatureKeys.BudgetAccess,
                AppFeatureKeys.ScenariosAccess,
                AppFeatureKeys.ReportsAccess
            },
            [UserRole.Advanced] = new(StringComparer.OrdinalIgnoreCase)
            {
                AppFeatureKeys.AuthSecurityManage,
                AppFeatureKeys.BillingManage,
                AppFeatureKeys.SubscriptionsManage,
                AppFeatureKeys.ProfileManage,
                AppFeatureKeys.PreferencesManage,
                AppFeatureKeys.SpacesManage,
                AppFeatureKeys.OnboardingManage,
                AppFeatureKeys.NotificationsAccess,
                AppFeatureKeys.LookupsRead,
                AppFeatureKeys.DataPortabilityExport,
                AppFeatureKeys.DataPortabilityImport,
                AppFeatureKeys.PlansManage,
                AppFeatureKeys.IncomesRead,
                AppFeatureKeys.IncomesSummaryRead,
                AppFeatureKeys.GoalsManage,
                AppFeatureKeys.GoalContributionsManage,
                AppFeatureKeys.InstallmentsRead,
                AppFeatureKeys.InstallmentsPay,
                AppFeatureKeys.InstallmentsManage,
                AppFeatureKeys.InstallmentsAnticipate,
                AppFeatureKeys.AccountsRead,
                AppFeatureKeys.AccountsManage,
                AppFeatureKeys.AccountsAnalytics,
                AppFeatureKeys.AccountsImport,
                AppFeatureKeys.CardsRead,
                AppFeatureKeys.CardsCreateUpdate,
                AppFeatureKeys.CardsDelete,
                AppFeatureKeys.CardsStatementsRead,
                AppFeatureKeys.CategoriesRead,
                AppFeatureKeys.CategoriesManage,
                AppFeatureKeys.InvoiceImportAccess,
                AppFeatureKeys.FinancialAssistantAccess,
                AppFeatureKeys.MonthlySnapshotsAccess,
                AppFeatureKeys.LoansAccess,
                AppFeatureKeys.InvestmentsAccess,
                AppFeatureKeys.BudgetAccess,
                AppFeatureKeys.ScenariosAccess,
                AppFeatureKeys.ReportsAccess
            },
            [UserRole.Admin] = new(StringComparer.OrdinalIgnoreCase)
            {
                AppFeatureKeys.AuthSecurityManage,
                AppFeatureKeys.BillingManage,
                AppFeatureKeys.SubscriptionsManage,
                AppFeatureKeys.ProfileManage,
                AppFeatureKeys.PreferencesManage,
                AppFeatureKeys.SpacesManage,
                AppFeatureKeys.OnboardingManage,
                AppFeatureKeys.NotificationsAccess,
                AppFeatureKeys.LookupsRead,
                AppFeatureKeys.DataPortabilityExport,
                AppFeatureKeys.DataPortabilityImport,
                AppFeatureKeys.PlansManage,
                AppFeatureKeys.IncomesRead,
                AppFeatureKeys.IncomesSummaryRead,
                AppFeatureKeys.GoalsManage,
                AppFeatureKeys.GoalContributionsManage,
                AppFeatureKeys.InstallmentsRead,
                AppFeatureKeys.InstallmentsPay,
                AppFeatureKeys.InstallmentsManage,
                AppFeatureKeys.InstallmentsAnticipate,
                AppFeatureKeys.AccountsRead,
                AppFeatureKeys.AccountsManage,
                AppFeatureKeys.AccountsAnalytics,
                AppFeatureKeys.AccountsImport,
                AppFeatureKeys.CardsRead,
                AppFeatureKeys.CardsCreateUpdate,
                AppFeatureKeys.CardsDelete,
                AppFeatureKeys.CardsStatementsRead,
                AppFeatureKeys.CategoriesRead,
                AppFeatureKeys.CategoriesManage,
                AppFeatureKeys.InvoiceImportAccess,
                AppFeatureKeys.FinancialAssistantAccess,
                AppFeatureKeys.MonthlySnapshotsAccess,
                AppFeatureKeys.LoansAccess,
                AppFeatureKeys.InvestmentsAccess,
                AppFeatureKeys.AdminUsersManage,
                AppFeatureKeys.AdminParametersManage,
                AppFeatureKeys.AdminRobotsManage,
                AppFeatureKeys.AdminCategoriesManage
            }
        };

    public static IReadOnlyCollection<string> GetRoleFeatures(UserRole role)
    {
        if (role == UserRole.Admin)
            return AppFeatureKeys.All.ToArray();

        return Matrix.TryGetValue(role, out var features) ? features.ToArray() : [];
    }

    public static bool HasRoleFeature(UserRole role, string featureKey)
    {
        if (string.IsNullOrWhiteSpace(featureKey))
            return false;

        if (role == UserRole.Admin)
            return true;

        return Matrix.TryGetValue(role, out var allowed) && allowed.Contains(featureKey);
    }
}
