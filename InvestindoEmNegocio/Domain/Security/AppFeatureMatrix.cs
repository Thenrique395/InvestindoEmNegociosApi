using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Security;

public static class AppFeatureMatrix
{
    private static readonly IReadOnlyDictionary<UserRole, HashSet<string>> Matrix =
        new Dictionary<UserRole, HashSet<string>>
        {
            [UserRole.Basic] = new(StringComparer.OrdinalIgnoreCase)
            {
                AppFeatureKeys.CardsAccess,
                AppFeatureKeys.AccountsAccess,
                AppFeatureKeys.CategoriesAccess
            },
            [UserRole.Intermediate] = new(StringComparer.OrdinalIgnoreCase)
            {
                AppFeatureKeys.CardsAccess,
                AppFeatureKeys.AccountsAccess,
                AppFeatureKeys.CategoriesAccess,
                AppFeatureKeys.InvoiceImportAccess
            },
            [UserRole.Advanced] = new(StringComparer.OrdinalIgnoreCase)
            {
                AppFeatureKeys.CardsAccess,
                AppFeatureKeys.AccountsAccess,
                AppFeatureKeys.CategoriesAccess,
                AppFeatureKeys.InvoiceImportAccess,
                AppFeatureKeys.InvestmentsAccess
            },
            [UserRole.Admin] = new(StringComparer.OrdinalIgnoreCase)
            {
                AppFeatureKeys.CardsAccess,
                AppFeatureKeys.AccountsAccess,
                AppFeatureKeys.CategoriesAccess,
                AppFeatureKeys.InvoiceImportAccess,
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

