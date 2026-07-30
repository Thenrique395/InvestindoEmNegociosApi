using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class LoanAmortizationConfiguration : IEntityTypeConfiguration<LoanAmortization>
{
    public void Configure(EntityTypeBuilder<LoanAmortization> builder)
    {
        builder.ToTable("loan_amortizations");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Amount).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(a => a.PreviousBalance).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(a => a.NewBalance).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(a => a.PreviousPayment).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(a => a.NewPayment).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(a => a.EstimatedInterestBefore).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(a => a.EstimatedInterestAfter).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(a => a.EstimatedSavings).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(a => a.Strategy).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(a => a.PreviousTerm).IsRequired();
        builder.Property(a => a.NewTerm).IsRequired();
        builder.Property(a => a.ScheduleVersion).IsRequired();
        builder.Property(a => a.EffectiveDate).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.SpaceId).IsRequired();
        builder.Property(a => a.Note).HasMaxLength(200);
        builder.Property(a => a.ReceiptUrl).HasMaxLength(500);
        builder.Property(a => a.ReversalReason).HasMaxLength(300);
        builder.Property(a => a.IdempotencyKey).HasMaxLength(120).IsRequired();

        builder.HasIndex(a => new { a.UserId, a.CreatedAt });
        builder.HasIndex(a => a.ContractId);
        builder.HasIndex(a => new { a.UserId, a.IdempotencyKey }).IsUnique();

        builder.HasOne<LoanContract>()
            .WithMany()
            .HasForeignKey(a => a.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(a => a.AccountId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasCheckConstraint("ck_loan_amortization_amount_positive", "\"Amount\" > 0");

        builder.HasQueryFilter(a => a.DeletedAt == null);
    }
}
