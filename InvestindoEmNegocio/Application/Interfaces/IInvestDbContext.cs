using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IInvestDbContext
{
    DbSet<User> Users { get; }
    DbSet<BillingCheckout> BillingCheckouts { get; }
    DbSet<BillingWebhookEvent> BillingWebhookEvents { get; }
    DbSet<UserProfile> UserProfiles { get; }
    DbSet<Category> Categories { get; }
    DbSet<UserCategorizationFeedback> UserCategorizationFeedback { get; }
    DbSet<Card> Cards { get; }
    DbSet<Account> Accounts { get; }
    DbSet<AccountTransaction> AccountTransactions { get; }
    DbSet<Goal> Goals { get; }
    DbSet<GoalContribution> GoalContributions { get; }
    DbSet<MoneyPlan> MoneyPlans { get; }
    DbSet<MoneyInstallment> MoneyInstallments { get; }
    DbSet<MoneyPayment> MoneyPayments { get; }
    DbSet<InvestmentGoal> InvestmentGoals { get; }
    DbSet<InvestmentAllocationTarget> InvestmentAllocationTargets { get; }
    DbSet<InvestmentPosition> InvestmentPositions { get; }
    DbSet<InvestmentMovement> InvestmentMovements { get; }
    DbSet<UserSubscription> UserSubscriptions { get; }
    DbSet<UserOnboarding> UserOnboardings { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<UserNotification> UserNotifications { get; }
    DbSet<RobotExecutionLog> RobotExecutionLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
