using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class GoalContributionConfiguration : IEntityTypeConfiguration<GoalContribution>
{
    public void Configure(EntityTypeBuilder<GoalContribution> builder)
    {
        builder.ToTable("goal_contributions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount).HasColumnType("numeric(14,2)");
        builder.Property(x => x.Note).HasMaxLength(300);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.GoalId, x.Date });
    }
}
