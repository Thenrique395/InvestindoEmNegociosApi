using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class GoalScopeConfiguration : IEntityTypeConfiguration<GoalScope>
{
    public void Configure(EntityTypeBuilder<GoalScope> builder)
    {
        builder.ToTable("goal_scopes");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ScopeType)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(s => s.RefId).IsRequired();
        builder.Property(s => s.GoalId).IsRequired();

        builder.HasIndex(s => s.GoalId);
        builder.HasIndex(s => new { s.GoalId, s.ScopeType });
    }
}
