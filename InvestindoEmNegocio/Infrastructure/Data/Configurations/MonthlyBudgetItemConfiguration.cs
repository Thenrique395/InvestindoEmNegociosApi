using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class MonthlyBudgetItemConfiguration : IEntityTypeConfiguration<MonthlyBudgetItem>
{
    public void Configure(EntityTypeBuilder<MonthlyBudgetItem> builder)
    {
        builder.ToTable("monthly_budget_items");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.CategoryName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.PlannedAmount)
            .HasColumnType("numeric(14,2)")
            .IsRequired();

        builder.Property(b => b.Year).IsRequired();
        builder.Property(b => b.Month).IsRequired();
        builder.Property(b => b.CreatedAt).IsRequired();
        builder.Property(b => b.UpdatedAt).IsRequired();

        builder.HasIndex(b => new { b.UserId, b.Year, b.Month });
        builder.HasIndex(b => new { b.UserId, b.Year, b.Month, b.CategoryName }).IsUnique();
    }
}
