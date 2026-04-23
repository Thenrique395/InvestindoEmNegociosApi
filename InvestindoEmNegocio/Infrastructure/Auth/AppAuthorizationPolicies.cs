using System.Security.Claims;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Security;
using Microsoft.AspNetCore.Authorization;

namespace InvestindoEmNegocio.Infrastructure.Auth;

public static class AppAuthorizationPolicies
{
    public const string AdminOnly = "admin.only";
    public const string AtLeastBasic = "role.atLeast.basic";
    public const string AtLeastIntermediate = "role.atLeast.intermediate";
    public const string AtLeastAdvanced = "role.atLeast.advanced";
    public const string FeatureAuthSecurityManage = "feature.auth.security.manage";
    public const string FeatureBillingManage = "feature.billing.manage";
    public const string FeatureSubscriptionsManage = "feature.subscriptions.manage";
    public const string FeatureProfileManage = "feature.profile.manage";
    public const string FeaturePreferencesManage = "feature.preferences.manage";
    public const string FeatureOnboardingManage = "feature.onboarding.manage";
    public const string FeatureNotificationsAccess = "feature.notifications.access";
    public const string FeatureLookupsRead = "feature.lookups.read";
    public const string FeatureDataPortabilityExport = "feature.data-portability.export";
    public const string FeatureDataPortabilityImport = "feature.data-portability.import";
    public const string FeaturePlansManage = "feature.plans.manage";
    public const string FeatureIncomesSummaryRead = "feature.incomes.summary.read";
    public const string FeatureGoalsManage = "feature.goals.manage";
    public const string FeatureGoalContributionsManage = "feature.goal-contributions.manage";
    public const string FeatureInstallmentsRead = "feature.installments.read";
    public const string FeatureInstallmentsPay = "feature.installments.pay";
    public const string FeatureInstallmentsManage = "feature.installments.manage";
    public const string FeatureInstallmentsAnticipate = "feature.installments.anticipate";
    public const string FeatureInvestmentsAccess = "feature.investments.access";
    public const string FeatureAccountsRead = "feature.accounts.read";
    public const string FeatureAccountsManage = "feature.accounts.manage";
    public const string FeatureAccountsAnalytics = "feature.accounts.analytics";
    public const string FeatureAccountsImport = "feature.accounts.import";
    public const string FeatureCardsRead = "feature.cards.read";
    public const string FeatureCardsManage = "feature.cards.manage";
    public const string FeatureCardsStatements = "feature.cards.statements";
    public const string FeatureCategoriesRead = "feature.categories.read";
    public const string FeatureCategoriesManage = "feature.categories.manage";
    public const string FeatureInvoiceImportAccess = "feature.invoice-import.access";
    public const string FeatureFinancialAssistantAccess = "feature.financial-assistant.access";
    public const string FeatureMonthlySnapshotsAccess = "feature.monthly-snapshots.access";
    public const string FeatureLoansAccess = "feature.loans.access";
    public const string FeatureAdminUsersManage = "feature.admin.users.manage";
    public const string FeatureAdminParametersManage = "feature.admin.parameters.manage";
    public const string FeatureAdminRobotsManage = "feature.admin.robots.manage";
    public const string FeatureAdminCategoriesManage = "feature.admin.categories.manage";

    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(AdminOnly, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole(UserRole.Admin.ToString());
        });

        options.AddPolicy(AtLeastBasic, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context => HasAtLeastRole(context.User, UserRole.Basic));
        });

        options.AddPolicy(AtLeastIntermediate, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context => HasAtLeastRole(context.User, UserRole.Intermediate));
        });

        options.AddPolicy(AtLeastAdvanced, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context => HasAtLeastRole(context.User, UserRole.Advanced));
        });

        AddFeaturePolicy(options, FeatureAuthSecurityManage, AppFeatureKeys.AuthSecurityManage);
        AddFeaturePolicy(options, FeatureBillingManage, AppFeatureKeys.BillingManage);
        AddFeaturePolicy(options, FeatureSubscriptionsManage, AppFeatureKeys.SubscriptionsManage);
        AddFeaturePolicy(options, FeatureProfileManage, AppFeatureKeys.ProfileManage);
        AddFeaturePolicy(options, FeaturePreferencesManage, AppFeatureKeys.PreferencesManage);
        AddFeaturePolicy(options, FeatureOnboardingManage, AppFeatureKeys.OnboardingManage);
        AddFeaturePolicy(options, FeatureNotificationsAccess, AppFeatureKeys.NotificationsAccess);
        AddFeaturePolicy(options, FeatureLookupsRead, AppFeatureKeys.LookupsRead);
        AddFeaturePolicy(options, FeatureDataPortabilityExport, AppFeatureKeys.DataPortabilityExport);
        AddFeaturePolicy(options, FeatureDataPortabilityImport, AppFeatureKeys.DataPortabilityImport);
        AddFeaturePolicy(options, FeaturePlansManage, AppFeatureKeys.PlansManage);
        AddFeaturePolicy(options, FeatureIncomesSummaryRead, AppFeatureKeys.IncomesSummaryRead);
        AddFeaturePolicy(options, FeatureGoalsManage, AppFeatureKeys.GoalsManage);
        AddFeaturePolicy(options, FeatureGoalContributionsManage, AppFeatureKeys.GoalContributionsManage);
        AddFeaturePolicy(options, FeatureInstallmentsRead, AppFeatureKeys.InstallmentsRead);
        AddFeaturePolicy(options, FeatureInstallmentsPay, AppFeatureKeys.InstallmentsPay);
        AddFeaturePolicy(options, FeatureInstallmentsManage, AppFeatureKeys.InstallmentsManage);
        AddFeaturePolicy(options, FeatureInstallmentsAnticipate, AppFeatureKeys.InstallmentsAnticipate);
        AddFeaturePolicy(options, FeatureInvestmentsAccess, AppFeatureKeys.InvestmentsAccess);
        AddFeaturePolicy(options, FeatureAccountsRead, AppFeatureKeys.AccountsRead);
        AddFeaturePolicy(options, FeatureAccountsManage, AppFeatureKeys.AccountsManage);
        AddFeaturePolicy(options, FeatureAccountsAnalytics, AppFeatureKeys.AccountsAnalytics);
        AddFeaturePolicy(options, FeatureAccountsImport, AppFeatureKeys.AccountsImport);
        AddFeaturePolicy(options, FeatureCardsRead, AppFeatureKeys.CardsRead);
        AddFeaturePolicy(options, FeatureCardsManage, AppFeatureKeys.CardsManage);
        AddFeaturePolicy(options, FeatureCardsStatements, AppFeatureKeys.CardsStatements);
        AddFeaturePolicy(options, FeatureCategoriesRead, AppFeatureKeys.CategoriesRead);
        AddFeaturePolicy(options, FeatureCategoriesManage, AppFeatureKeys.CategoriesManage);
        AddFeaturePolicy(options, FeatureInvoiceImportAccess, AppFeatureKeys.InvoiceImportAccess);
        AddFeaturePolicy(options, FeatureFinancialAssistantAccess, AppFeatureKeys.FinancialAssistantAccess);
        AddFeaturePolicy(options, FeatureMonthlySnapshotsAccess, AppFeatureKeys.MonthlySnapshotsAccess);
        AddFeaturePolicy(options, FeatureLoansAccess, AppFeatureKeys.LoansAccess);
        AddFeaturePolicy(options, FeatureAdminUsersManage, AppFeatureKeys.AdminUsersManage);
        AddFeaturePolicy(options, FeatureAdminParametersManage, AppFeatureKeys.AdminParametersManage);
        AddFeaturePolicy(options, FeatureAdminRobotsManage, AppFeatureKeys.AdminRobotsManage);
        AddFeaturePolicy(options, FeatureAdminCategoriesManage, AppFeatureKeys.AdminCategoriesManage);
    }

    private static void AddFeaturePolicy(AuthorizationOptions options, string policyName, string featureKey)
    {
        options.AddPolicy(policyName, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context => FeatureAccessEvaluator.HasFeature(context.User, featureKey));
        });
    }

    private static bool HasAtLeastRole(ClaimsPrincipal user, UserRole minimumRole)
    {
        var currentRole = FeatureAccessEvaluator.ResolveRole(user);
        if (currentRole is null)
            return false;

        return currentRole.Value >= minimumRole;
    }
}
