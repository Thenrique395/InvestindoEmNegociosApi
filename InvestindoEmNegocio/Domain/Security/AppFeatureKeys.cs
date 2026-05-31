namespace InvestindoEmNegocio.Domain.Security;

public static class AppFeatureKeys
{
    public const string AuthSecurityManage = "auth.security.manage";
    public const string BillingManage = "billing.manage";
    public const string SubscriptionsManage = "subscriptions.manage";
    public const string ProfileManage = "profile.manage";
    public const string PreferencesManage = "preferences.manage";
    public const string OnboardingManage = "onboarding.manage";
    public const string NotificationsAccess = "notifications.access";
    public const string LookupsRead = "lookups.read";
    public const string DataPortabilityExport = "data-portability.export";
    public const string DataPortabilityImport = "data-portability.import";
    public const string PlansManage = "plans.manage";
    public const string IncomesSummaryRead = "incomes.summary.read";
    public const string GoalsManage = "goals.manage";
    public const string GoalContributionsManage = "goal-contributions.manage";
    public const string InstallmentsRead = "installments.read";
    public const string InstallmentsPay = "installments.pay";
    public const string InstallmentsManage = "installments.manage";
    public const string InstallmentsAnticipate = "installments.anticipate";
    public const string InvestmentsAccess = "investments.access";
    public const string AccountsRead = "accounts.read";
    public const string AccountsManage = "accounts.manage";
    public const string AccountsAnalytics = "accounts.analytics";
    public const string AccountsImport = "accounts.import";
    public const string CardsRead = "cards.read";
    public const string CardsCreateUpdate = "cards.create-update";
    public const string CardsAdminManage = "cards.manage";
    public const string CardsStatementsRead = "cards.statements";
    public const string CategoriesRead = "categories.read";
    public const string CategoriesManage = "categories.manage";
    public const string InvoiceImportAccess = "invoice-import.access";
    public const string FinancialAssistantAccess = "financial-assistant.access";
    public const string MonthlySnapshotsAccess = "monthly-snapshots.access";
    public const string LoansAccess = "loans.access";
    public const string AdminUsersManage = "admin.users.manage";
    public const string AdminParametersManage = "admin.parameters.manage";
    public const string AdminRobotsManage = "admin.robots.manage";
    public const string AdminCategoriesManage = "admin.categories.manage";

    public static readonly IReadOnlyList<string> All =
    [
        AuthSecurityManage,
        BillingManage,
        SubscriptionsManage,
        ProfileManage,
        PreferencesManage,
        OnboardingManage,
        NotificationsAccess,
        LookupsRead,
        DataPortabilityExport,
        DataPortabilityImport,
        PlansManage,
        IncomesSummaryRead,
        GoalsManage,
        GoalContributionsManage,
        InstallmentsRead,
        InstallmentsPay,
        InstallmentsManage,
        InstallmentsAnticipate,
        InvestmentsAccess,
        AccountsRead,
        AccountsManage,
        AccountsAnalytics,
        AccountsImport,
        CardsRead,
        CardsCreateUpdate,
        CardsAdminManage,
        CardsStatementsRead,
        CategoriesRead,
        CategoriesManage,
        InvoiceImportAccess,
        FinancialAssistantAccess,
        MonthlySnapshotsAccess,
        LoansAccess,
        AdminUsersManage,
        AdminParametersManage,
        AdminRobotsManage,
        AdminCategoriesManage
    ];

    public static bool IsKnownFeature(string featureKey)
        => All.Contains(featureKey, StringComparer.OrdinalIgnoreCase);
}
