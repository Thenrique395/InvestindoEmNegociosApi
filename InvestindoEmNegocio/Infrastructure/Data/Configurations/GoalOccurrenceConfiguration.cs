using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class GoalOccurrenceConfiguration : IEntityTypeConfiguration<GoalOccurrence>
{
    public void Configure(EntityTypeBuilder<GoalOccurrence> builder)
    {
        builder.ToTable("goal_occurrences");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.GoalId).IsRequired();
        builder.Property(o => o.Sequence).IsRequired();
        builder.Property(o => o.PeriodStart).IsRequired();
        builder.Property(o => o.PeriodEnd).IsRequired();
        builder.Property(o => o.TargetAmount).HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(o => o.Status).HasConversion<string>().IsRequired();
        builder.Property(o => o.ClosedAt);
        builder.Property(o => o.CreatedAt).IsRequired();

        builder.HasOne<Goal>()
            .WithMany()
            .HasForeignKey(o => o.GoalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => o.GoalId);
        builder.HasIndex(o => new { o.GoalId, o.PeriodStart });
        builder.HasIndex(o => new { o.GoalId, o.Sequence }).IsUnique();
    }
}
