using FluentAssertions;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Tests;

public class DomainEntitiesTests
{
    [Fact]
    public void Expense_And_Income_Should_Normalize_Fields_And_Use_Utc_Dates()
    {
        var localDate = DateTime.SpecifyKind(new DateTime(2026, 2, 21, 10, 30, 0), DateTimeKind.Local);
        var userId = Guid.NewGuid();

        var expense = new Expense(userId, "  Mercado  ", "  Alimentação ", 120.50m, localDate);
        var income = new Income(userId, "  Salário ", 5000m, localDate, fixa: true, fixaInicio: "2026-01", fixaFim: "2026-12");

        expense.Nome.Should().Be("Mercado");
        expense.Categoria.Should().Be("Alimentação");
        expense.Vencimento.Kind.Should().Be(DateTimeKind.Utc);

        income.Fonte.Should().Be("Salário");
        income.Recebimento.Kind.Should().Be(DateTimeKind.Utc);
        income.Fixa.Should().BeTrue();
    }

    [Fact]
    public void Goal_AddContribution_Should_Move_Status_And_Cap_At_Target()
    {
        var goal = new Goal(Guid.NewGuid(), "Reserva", targetAmount: 1000m, year: 2026);

        goal.AddContribution(200m);
        goal.CurrentAmount.Should().Be(200m);
        goal.Status.Should().Be(GoalStatus.InProgress);

        goal.AddContribution(900m);
        goal.CurrentAmount.Should().Be(1000m);
        goal.Status.Should().Be(GoalStatus.Completed);
    }

    [Fact]
    public void Goal_AddContribution_Should_Not_Change_When_Amount_Is_Not_Positive()
    {
        var goal = new Goal(Guid.NewGuid(), "Viagem", targetAmount: 500m, year: 2026, currentAmount: 100m, status: GoalStatus.Planned);

        goal.AddContribution(0m);
        goal.AddContribution(-10m);

        goal.CurrentAmount.Should().Be(100m);
        goal.Status.Should().Be(GoalStatus.Planned);
    }

    [Fact]
    public void User_Should_Apply_Lockout_After_Max_Attempts_And_Reset_After_Success()
    {
        var user = new User("Henrique", "henrique@test.com", "hash");
        var now = DateTime.UtcNow;

        user.RegisterFailedLogin(now, maxAttempts: 3, lockoutDuration: TimeSpan.FromMinutes(15));
        user.RegisterFailedLogin(now.AddMinutes(1), maxAttempts: 3, lockoutDuration: TimeSpan.FromMinutes(15));

        user.FailedLoginAttempts.Should().Be(2);
        user.LockoutUntil.Should().BeNull();

        user.RegisterFailedLogin(now.AddMinutes(2), maxAttempts: 3, lockoutDuration: TimeSpan.FromMinutes(15));

        user.FailedLoginAttempts.Should().Be(0);
        user.LockoutUntil.Should().NotBeNull();
        user.IsLocked(now.AddMinutes(10)).Should().BeTrue();

        user.ResetFailedLogins(now.AddMinutes(11));
        user.FailedLoginAttempts.Should().Be(0);
        user.LockoutUntil.Should().BeNull();
    }

    [Fact]
    public void User_Should_Update_Status_Role_And_Password()
    {
        var user = new User("Henrique", "henrique@test.com", "hash");

        user.SetRole(UserRole.Admin);
        user.ChangePassword("new-hash");
        user.Deactivate();
        user.Activate();

        user.Role.Should().Be(UserRole.Admin);
        user.PasswordHash.Should().Be("new-hash");
        user.IsActive.Should().BeTrue();
    }
}
