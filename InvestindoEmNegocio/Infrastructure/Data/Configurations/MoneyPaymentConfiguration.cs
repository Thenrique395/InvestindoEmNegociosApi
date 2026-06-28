using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class MoneyPaymentConfiguration : IEntityTypeConfiguration<MoneyPayment>
{
    [Obsolete("Obsolete")]
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
        builder.Property(p => p.DeletedAt);

        builder.Property(p => p.Note)
            .HasMaxLength(200);

        builder.Property(p => p.ReceiptUrl)
            .HasMaxLength(500);

        builder.Property(p => p.SpaceId).IsRequired();

        builder.HasIndex(p => new { p.UserId, p.PaidAt });
        builder.HasIndex(p => p.InstallmentId);
        builder.HasIndex(p => p.AccountId);

        builder.HasOne<MoneyInstallment>()
            .WithMany()
            .HasForeignKey(p => p.InstallmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(p => p.AccountId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasCheckConstraint("ck_payment_amount_positive", "\"PaidAmount\" > 0");

        builder.HasQueryFilter(p => p.DeletedAt == null);
    }
}
