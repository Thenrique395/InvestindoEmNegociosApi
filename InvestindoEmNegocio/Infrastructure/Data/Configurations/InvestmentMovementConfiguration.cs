using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class InvestmentMovementConfiguration : IEntityTypeConfiguration<InvestmentMovement>
{
    public void Configure(EntityTypeBuilder<InvestmentMovement> builder)
    {
        builder.ToTable("investment_movements");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(18, 6);
        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.Property(x => x.Date).HasColumnType("date");
        builder.Property(x => x.Note).HasMaxLength(400);

        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.PositionId);
    }
}
