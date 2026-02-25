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
    public const string FeatureInvestmentsAccess = "feature.investments.access";
    public const string FeatureCardsAccess = "feature.cards.access";
    public const string FeatureAccountsAccess = "feature.accounts.access";
    public const string FeatureCategoriesAccess = "feature.categories.access";
    public const string FeatureInvoiceImportAccess = "feature.invoice-import.access";
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

        options.AddPolicy(FeatureInvestmentsAccess, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context => FeatureAccessEvaluator.HasFeature(context.User, AppFeatureKeys.InvestmentsAccess));
        });

        options.AddPolicy(FeatureCardsAccess, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context => FeatureAccessEvaluator.HasFeature(context.User, AppFeatureKeys.CardsAccess));
        });

        options.AddPolicy(FeatureAccountsAccess, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context => FeatureAccessEvaluator.HasFeature(context.User, AppFeatureKeys.AccountsAccess));
        });

        options.AddPolicy(FeatureCategoriesAccess, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context => FeatureAccessEvaluator.HasFeature(context.User, AppFeatureKeys.CategoriesAccess));
        });

        options.AddPolicy(FeatureInvoiceImportAccess, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context => FeatureAccessEvaluator.HasFeature(context.User, AppFeatureKeys.InvoiceImportAccess));
        });

        options.AddPolicy(FeatureAdminUsersManage, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context => FeatureAccessEvaluator.HasFeature(context.User, AppFeatureKeys.AdminUsersManage));
        });

        options.AddPolicy(FeatureAdminParametersManage, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context => FeatureAccessEvaluator.HasFeature(context.User, AppFeatureKeys.AdminParametersManage));
        });

        options.AddPolicy(FeatureAdminRobotsManage, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context => FeatureAccessEvaluator.HasFeature(context.User, AppFeatureKeys.AdminRobotsManage));
        });

        options.AddPolicy(FeatureAdminCategoriesManage, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context => FeatureAccessEvaluator.HasFeature(context.User, AppFeatureKeys.AdminCategoriesManage));
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
