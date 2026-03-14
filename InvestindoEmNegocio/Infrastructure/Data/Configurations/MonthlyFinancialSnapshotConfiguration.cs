using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class MonthlyFinancialSnapshotConfiguration : IEntityTypeConfiguration<MonthlyFinancialSnapshot>
{
    public void Configure(EntityTypeBuilder<MonthlyFinancialSnapshot> builder)
    {
        builder.ToTable("monthly_financial_snapshots");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SnapshotLabel).IsRequired().HasMaxLength(7);
        builder.Property(x => x.RealAvailableBalance).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.ProjectedBalance).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.PendingExpenses).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.PendingIncomes).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.TotalDebt).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.NetWorth).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(x => x.RiskClassification).IsRequired().HasMaxLength(40);
        builder.Property(x => x.PrimaryInsight).IsRequired().HasMaxLength(500);
        builder.Property(x => x.RecommendationsJson).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.UserId, x.Year, x.Month }).IsUnique();
    }
}
