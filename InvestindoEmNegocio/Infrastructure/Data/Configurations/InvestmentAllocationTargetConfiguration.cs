using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class InvestmentAllocationTargetConfiguration : IEntityTypeConfiguration<InvestmentAllocationTarget>
{
    public void Configure(EntityTypeBuilder<InvestmentAllocationTarget> builder)
    {
        builder.ToTable("investment_allocation_targets");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.Rf).HasPrecision(5, 2).IsRequired();
        builder.Property(x => x.Acoes).HasPrecision(5, 2).IsRequired();
        builder.Property(x => x.Fundos).HasPrecision(5, 2).IsRequired();
        builder.Property(x => x.Cripto).HasPrecision(5, 2).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.UserId).IsUnique();
    }
}
