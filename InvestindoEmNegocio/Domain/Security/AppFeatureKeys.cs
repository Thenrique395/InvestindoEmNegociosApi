namespace InvestindoEmNegocio.Domain.Security;

public static class AppFeatureKeys
{
    public const string InvestmentsAccess = "investments.access";
    public const string CardsAccess = "cards.access";
    public const string AccountsAccess = "accounts.access";
    public const string CategoriesAccess = "categories.access";
    public const string InvoiceImportAccess = "invoice-import.access";
    public const string AdminUsersManage = "admin.users.manage";
    public const string AdminParametersManage = "admin.parameters.manage";
    public const string AdminRobotsManage = "admin.robots.manage";
    public const string AdminCategoriesManage = "admin.categories.manage";

    public static readonly IReadOnlyList<string> All =
    [
        InvestmentsAccess,
        CardsAccess,
        AccountsAccess,
        CategoriesAccess,
        InvoiceImportAccess,
        AdminUsersManage,
        AdminParametersManage,
        AdminRobotsManage,
        AdminCategoriesManage
    ];

    public static bool IsKnownFeature(string featureKey)
        => All.Contains(featureKey, StringComparer.OrdinalIgnoreCase);
}
