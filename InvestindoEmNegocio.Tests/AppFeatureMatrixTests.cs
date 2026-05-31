using FluentAssertions;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Security;

namespace InvestindoEmNegocio.Tests;

public class AppFeatureMatrixTests
{
    [Fact]
    public void Basic_Should_Receive_Common_Self_Service_Features()
    {
        string[] expectedFeatures =
        [
            AppFeatureKeys.AuthSecurityManage,
            AppFeatureKeys.BillingManage,
            AppFeatureKeys.SubscriptionsManage,
            AppFeatureKeys.ProfileManage,
            AppFeatureKeys.PreferencesManage,
            AppFeatureKeys.OnboardingManage,
            AppFeatureKeys.NotificationsAccess,
            AppFeatureKeys.LookupsRead,
            AppFeatureKeys.DataPortabilityExport,
            AppFeatureKeys.DataPortabilityImport,
            AppFeatureKeys.PlansManage,
            AppFeatureKeys.IncomesSummaryRead,
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
        ];

        foreach (var feature in expectedFeatures)
        {
            AppFeatureMatrix.HasRoleFeature(UserRole.Basic, feature).Should().BeTrue(feature);
        }
    }

    [Fact]
    public void Basic_Should_Not_Receive_Intermediate_Advanced_Or_Admin_Features()
    {
        string[] deniedFeatures =
        [
            AppFeatureKeys.InstallmentsAnticipate,
            AppFeatureKeys.AccountsManage,
            AppFeatureKeys.AccountsAnalytics,
            AppFeatureKeys.AccountsImport,
            AppFeatureKeys.CardsStatementsRead,
            AppFeatureKeys.CategoriesManage,
            AppFeatureKeys.InvoiceImportAccess,
            AppFeatureKeys.FinancialAssistantAccess,
            AppFeatureKeys.MonthlySnapshotsAccess,
            AppFeatureKeys.LoansAccess,
            AppFeatureKeys.InvestmentsAccess,
            AppFeatureKeys.AdminUsersManage
        ];

        foreach (var feature in deniedFeatures)
        {
            AppFeatureMatrix.HasRoleFeature(UserRole.Basic, feature).Should().BeFalse(feature);
        }
    }

    [Fact]
    public void Intermediate_Should_Receive_Operational_Month_Features_But_Not_Investments()
    {
        string[] expectedFeatures =
        [
            AppFeatureKeys.InstallmentsAnticipate,
            AppFeatureKeys.AccountsManage,
            AppFeatureKeys.AccountsAnalytics,
            AppFeatureKeys.AccountsImport,
            AppFeatureKeys.CardsRead,
            AppFeatureKeys.CardsCreateUpdate,
            AppFeatureKeys.CardsDelete,
            AppFeatureKeys.CardsStatementsRead,
            AppFeatureKeys.CategoriesManage,
            AppFeatureKeys.InvoiceImportAccess,
            AppFeatureKeys.FinancialAssistantAccess,
            AppFeatureKeys.MonthlySnapshotsAccess,
            AppFeatureKeys.LoansAccess
        ];

        foreach (var feature in expectedFeatures)
        {
            AppFeatureMatrix.HasRoleFeature(UserRole.Intermediate, feature).Should().BeTrue(feature);
        }

        AppFeatureMatrix.HasRoleFeature(UserRole.Intermediate, AppFeatureKeys.InvestmentsAccess).Should().BeFalse();
        AppFeatureMatrix.HasRoleFeature(UserRole.Intermediate, AppFeatureKeys.AdminUsersManage).Should().BeFalse();
    }

    [Fact]
    public void Advanced_Should_Receive_Investments()
    {
        AppFeatureMatrix.HasRoleFeature(UserRole.Advanced, AppFeatureKeys.InvestmentsAccess).Should().BeTrue();
    }

    [Fact]
    public void Admin_Should_Receive_All_Features()
    {
        AppFeatureKeys.All.All(feature => AppFeatureMatrix.HasRoleFeature(UserRole.Admin, feature)).Should().BeTrue();
    }
}
