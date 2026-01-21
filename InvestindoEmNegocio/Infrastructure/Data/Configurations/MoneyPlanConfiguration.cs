using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class MoneyPlanConfiguration : IEntityTypeConfiguration<MoneyPlan>
{
    public void Configure(EntityTypeBuilder<MoneyPlan> builder)
    {
        builder.ToTable("money_plans");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Type)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(p => p.Schedule)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(p => p.Frequency)
            .HasConversion<string?>();

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(p => p.Title)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(p => p.Amount)
            .HasColumnType("numeric(14,2)")
            .IsRequired();

        builder.Property(p => p.StartDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();

        builder.HasIndex(p => p.UserId);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Card>()
            .WithMany()
            .HasForeignKey(p => p.CardId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasCheckConstraint("ck_money_plans_schedule",
            "(\"Schedule\" = 'OneTime' AND \"InstallmentsCount\" = 1 AND \"Frequency\" IS NULL) OR " +
            "(\"Schedule\" = 'Installments' AND \"InstallmentsCount\" >= 2 AND \"Frequency\" IS NULL) OR " +
            "(\"Schedule\" = 'Recurring' AND \"InstallmentsCount\" IS NULL AND \"Frequency\" IS NOT NULL)");
    }
}
