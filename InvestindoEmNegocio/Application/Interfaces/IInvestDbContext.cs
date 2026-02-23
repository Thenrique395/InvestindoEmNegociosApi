using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IInvestDbContext
{
    DbSet<UserProfile> UserProfiles { get; }
    DbSet<Category> Categories { get; }
    DbSet<Card> Cards { get; }
    DbSet<Goal> Goals { get; }
    DbSet<GoalContribution> GoalContributions { get; }
    DbSet<MoneyPlan> MoneyPlans { get; }
    DbSet<MoneyInstallment> MoneyInstallments { get; }
    DbSet<MoneyPayment> MoneyPayments { get; }
    DbSet<InvestmentGoal> InvestmentGoals { get; }
    DbSet<InvestmentAllocationTarget> InvestmentAllocationTargets { get; }
    DbSet<InvestmentPosition> InvestmentPositions { get; }
    DbSet<InvestmentMovement> InvestmentMovements { get; }
    DbSet<UserOnboarding> UserOnboardings { get; }
    DbSet<UserNotification> UserNotifications { get; }
    DbSet<RobotExecutionLog> RobotExecutionLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
