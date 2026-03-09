using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace InvestindoEmNegocio.Infrastructure.Data;

public class InvestDbContext(DbContextOptions<InvestDbContext> options) : DbContext(options), IInvestDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserFeatureOverride> UserFeatureOverrides => Set<UserFeatureOverride>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<CardBrand> CardBrands => Set<CardBrand>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountTransaction> AccountTransactions => Set<AccountTransaction>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<UserCategorizationFeedback> UserCategorizationFeedback => Set<UserCategorizationFeedback>();
    public DbSet<MoneyPlan> MoneyPlans => Set<MoneyPlan>();
    public DbSet<MoneyInstallment> MoneyInstallments => Set<MoneyInstallment>();
    public DbSet<MoneyPayment> MoneyPayments => Set<MoneyPayment>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<GoalContribution> GoalContributions => Set<GoalContribution>();
    public DbSet<InvestmentGoal> InvestmentGoals => Set<InvestmentGoal>();
    public DbSet<InvestmentAllocationTarget> InvestmentAllocationTargets => Set<InvestmentAllocationTarget>();
    public DbSet<InvestmentPosition> InvestmentPositions => Set<InvestmentPosition>();
    public DbSet<InvestmentMovement> InvestmentMovements => Set<InvestmentMovement>();
    public DbSet<UserOnboarding> UserOnboardings => Set<UserOnboarding>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<NotificationSettings> NotificationSettings => Set<NotificationSettings>();
    public DbSet<RobotSettings> RobotSettings => Set<RobotSettings>();
    public DbSet<RobotExecutionLog> RobotExecutionLogs => Set<RobotExecutionLog>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Database.BeginTransactionAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InvestDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
