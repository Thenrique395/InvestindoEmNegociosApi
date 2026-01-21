using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> builder)
    {
        builder.ToTable("goals");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(g => g.Description)
            .HasMaxLength(1000);

        builder.Property(g => g.TargetAmount)
            .HasColumnType("numeric(14,2)")
            .IsRequired();

        builder.Property(g => g.CurrentAmount)
            .HasColumnType("numeric(14,2)")
            .HasDefaultValue(0);

        builder.Property(g => g.ExpectedMonthly)
            .HasColumnType("numeric(14,2)")
            .HasDefaultValue(0);

        builder.Property(g => g.TargetDate);

        builder.Property(g => g.Year)
            .IsRequired();

        builder.Property(g => g.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(g => g.CreatedAt).IsRequired();
        builder.Property(g => g.UpdatedAt).IsRequired();

        builder.HasIndex(g => new { g.UserId, g.Year });
        builder.HasIndex(g => new { g.UserId, g.Status });
    }
}
