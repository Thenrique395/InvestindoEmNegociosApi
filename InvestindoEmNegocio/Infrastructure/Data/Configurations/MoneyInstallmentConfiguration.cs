using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class MoneyInstallmentConfiguration : IEntityTypeConfiguration<MoneyInstallment>
{
    public void Configure(EntityTypeBuilder<MoneyInstallment> builder)
    {
        builder.ToTable("money_installments");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(i => i.Amount)
            .HasColumnType("numeric(14,2)")
            .IsRequired();

        builder.Property(i => i.DueDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(i => i.OriginalDueDate)
            .HasColumnType("date");

        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.UpdatedAt).IsRequired();

        builder.HasIndex(i => new { i.UserId, i.DueDate });
        builder.HasIndex(i => new { i.PlanId, i.DueDate });

        builder.HasOne<MoneyPlan>()
            .WithMany()
            .HasForeignKey(i => i.PlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasCheckConstraint("ck_installment_no_positive", "\"InstallmentNo\" >= 1");
        builder.HasCheckConstraint("ck_installment_amount_positive", "\"Amount\" > 0");

        builder.HasIndex(i => new { i.PlanId, i.InstallmentNo }).IsUnique();
    }
}
