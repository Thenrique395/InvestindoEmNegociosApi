using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class InvestmentPositionConfiguration : IEntityTypeConfiguration<InvestmentPosition>
{
    public void Configure(EntityTypeBuilder<InvestmentPosition> builder)
    {
        builder.ToTable("investment_positions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.Type)
            .HasConversion<string>()
            .IsRequired();
        builder.Property(x => x.Asset).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Quantity).HasPrecision(18, 6);
        builder.Property(x => x.AvgPrice).HasPrecision(18, 2);
        builder.Property(x => x.OpenedAt).HasColumnType("date");
        builder.Property(x => x.Account).HasMaxLength(120);
        builder.Property(x => x.Category).HasMaxLength(80);
        builder.Property(x => x.Note).HasMaxLength(400);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasMany(x => x.Movements)
            .WithOne(x => x.Position)
            .HasForeignKey(x => x.PositionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.UserId, x.Asset });
    }
}
