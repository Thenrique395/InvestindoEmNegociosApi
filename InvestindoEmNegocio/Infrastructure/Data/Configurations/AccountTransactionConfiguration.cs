using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class AccountTransactionConfiguration : IEntityTypeConfiguration<AccountTransaction>
{
    [Obsolete("Obsolete")]
    public void Configure(EntityTypeBuilder<AccountTransaction> builder)
    {
        builder.ToTable("account_transactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Kind)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(t => t.Amount)
            .HasColumnType("numeric(14,2)")
            .IsRequired();

        builder.Property(t => t.Description)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.SourceType)
            .HasMaxLength(60);

        builder.Property(t => t.OccurredAt).IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.DeletedAt);

        builder.HasIndex(t => new { t.AccountId, t.OccurredAt });
        builder.HasIndex(t => new { t.UserId, t.OccurredAt });
        builder.HasIndex(t => new { t.SourceType, t.SourceId });

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasCheckConstraint("ck_account_transactions_amount_positive", "\"Amount\" > 0");

        builder.HasQueryFilter(t => t.DeletedAt == null);
    }
}
