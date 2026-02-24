using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    [Obsolete("Obsolete")]
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(a => a.Type)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(a => a.InitialBalance)
            .HasColumnType("numeric(14,2)")
            .IsRequired();

        builder.Property(a => a.IsActive)
            .IsRequired();

        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt).IsRequired();

        builder.HasIndex(a => new { a.UserId, a.Name }).IsUnique();
        builder.HasIndex(a => new { a.UserId, a.IsActive });

        builder.HasCheckConstraint("ck_accounts_initial_balance", "\"InitialBalance\" >= 0");
    }
}
