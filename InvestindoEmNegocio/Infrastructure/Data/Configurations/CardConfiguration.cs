using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> builder)
    {
        builder.ToTable("cards");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.HolderName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Nickname)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Last4)
            .IsRequired()
            .HasMaxLength(4);

        builder.Property(c => c.Bank)
            .HasMaxLength(120);

        builder.Property(c => c.CreditLimit)
            .HasColumnType("numeric(14,2)")
            .IsRequired();

        builder.Property(c => c.StatementCloseDay).IsRequired();
        builder.Property(c => c.DueDay).IsRequired();

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();

        builder.HasOne<CardBrand>()
            .WithMany()
            .HasForeignKey(c => c.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.UserId, c.Nickname }).IsUnique();
    }
}
