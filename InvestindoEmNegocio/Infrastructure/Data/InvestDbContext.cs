using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Data;

public class InvestDbContext : DbContext
{
    public InvestDbContext(DbContextOptions<InvestDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<CardBrand> CardBrands => Set<CardBrand>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<MoneyPlan> MoneyPlans => Set<MoneyPlan>();
    public DbSet<MoneyInstallment> MoneyInstallments => Set<MoneyInstallment>();
    public DbSet<MoneyPayment> MoneyPayments => Set<MoneyPayment>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<GoalContribution> GoalContributions => Set<GoalContribution>();
    public DbSet<InvestmentGoal> InvestmentGoals => Set<InvestmentGoal>();
    public DbSet<InvestmentPosition> InvestmentPositions => Set<InvestmentPosition>();
    public DbSet<InvestmentMovement> InvestmentMovements => Set<InvestmentMovement>();
    public DbSet<UserOnboarding> UserOnboardings => Set<UserOnboarding>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Institution> Institutions => Set<Institution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InvestDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
