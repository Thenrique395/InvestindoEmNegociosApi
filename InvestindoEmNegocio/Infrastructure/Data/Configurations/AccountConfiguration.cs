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

        builder.Property(a => a.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(a => a.SpaceId)
            .IsRequired();

        builder.Property(a => a.IsActive)
            .IsRequired();

        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt).IsRequired();
        builder.Property(a => a.DeletedAt);

        builder.HasIndex(a => new { a.UserId, a.SpaceId, a.Name }).IsUnique().HasFilter("\"DeletedAt\" IS NULL");
        builder.HasIndex(a => new { a.UserId, a.SpaceId, a.IsActive });

        builder.HasQueryFilter(a => a.DeletedAt == null);

        builder.HasCheckConstraint("ck_accounts_initial_balance", "\"InitialBalance\" >= 0");
    }
}
