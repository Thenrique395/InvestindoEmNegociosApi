using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class MoneyPaymentConfiguration : IEntityTypeConfiguration<MoneyPayment>
{
    public void Configure(EntityTypeBuilder<MoneyPayment> builder)
    {
        builder.ToTable("money_payments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PaidAmount)
            .HasColumnType("numeric(14,2)")
            .IsRequired();

        builder.Property(p => p.PaidAt)
            .IsRequired();

        builder.Property(p => p.CreatedAt).IsRequired();

        builder.Property(p => p.Note)
            .HasMaxLength(200);

        builder.HasIndex(p => new { p.UserId, p.PaidAt });
        builder.HasIndex(p => p.InstallmentId);

        builder.HasOne<MoneyInstallment>()
            .WithMany()
            .HasForeignKey(p => p.InstallmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasCheckConstraint("ck_payment_amount_positive", "\"PaidAmount\" > 0");
    }
}
