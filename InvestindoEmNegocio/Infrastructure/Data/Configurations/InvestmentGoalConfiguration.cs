using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class InvestmentGoalConfiguration : IEntityTypeConfiguration<InvestmentGoal>
{
    public void Configure(EntityTypeBuilder<InvestmentGoal> builder)
    {
        builder.ToTable("investment_goals");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.TargetAmount)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.UserId).IsUnique();
    }
}
