using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class LoanPaymentConfiguration : IEntityTypeConfiguration<LoanPayment>
{
    public void Configure(EntityTypeBuilder<LoanPayment> builder)
    {
        builder.ToTable("loan_payments");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Amount).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(p => p.PrincipalAmount).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(p => p.InterestAmount).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(p => p.PenaltyAmount).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(p => p.DiscountAmount).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(p => p.PaidAt).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.SpaceId).IsRequired();
        builder.Property(p => p.Note).HasMaxLength(200);
        builder.Property(p => p.ReceiptUrl).HasMaxLength(500);
        builder.Property(p => p.ReversalReason).HasMaxLength(300);
        builder.Property(p => p.IdempotencyKey).HasMaxLength(120).IsRequired();

        builder.HasIndex(p => new { p.UserId, p.PaidAt });
        builder.HasIndex(p => p.ContractId);
        builder.HasIndex(p => p.InstallmentId);
        builder.HasIndex(p => p.AccountId);
        // Idempotência: mesma chave por usuário nunca gera dois pagamentos.
        builder.HasIndex(p => new { p.UserId, p.IdempotencyKey }).IsUnique();

        builder.HasOne<LoanContract>()
            .WithMany()
            .HasForeignKey(p => p.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<LoanInstallment>()
            .WithMany()
            .HasForeignKey(p => p.InstallmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(p => p.AccountId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasCheckConstraint("ck_loan_payment_amount_positive", "\"Amount\" > 0");

        builder.HasQueryFilter(p => p.DeletedAt == null);
    }
}
