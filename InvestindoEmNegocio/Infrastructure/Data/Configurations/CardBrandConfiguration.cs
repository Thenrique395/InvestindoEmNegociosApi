using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class CardBrandConfiguration : IEntityTypeConfiguration<CardBrand>
{
    public void Configure(EntityTypeBuilder<CardBrand> builder)
    {
        builder.ToTable("card_brands");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(b => b.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(b => b.Code).IsUnique();

        builder.Property(b => b.IsActive)
            .IsRequired();

        // Seeds handled via migration
    }
}
