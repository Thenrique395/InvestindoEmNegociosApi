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

        // Sem HasDefaultValue nesses enums: o CLR default (GoalKind.General/
        // GoalMode.Limit/RecurrenceType.None = 0) é um valor de domínio válido, então
        // o EF trataria "valor 0 setado pelo app" como "não setado" e aplicaria o default
        // do banco. Isso CORROMPIA metas de despesa: Mode=Limit virava Target no insert.
        // A entidade sempre define esses valores no construtor/ConfigurePlanning.
        builder.Property(g => g.Kind)
            .HasConversion<string>()
            .IsRequired();

        // Fase A — planejamento
        builder.Property(g => g.Mode)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(g => g.Recurrence)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(g => g.StartDate);
        builder.Property(g => g.EndDate);
        builder.Property(g => g.WarningThreshold).HasColumnType("numeric(5,2)");
        builder.Property(g => g.CriticalThreshold).HasColumnType("numeric(5,2)");
        builder.Property(g => g.ArchivedAt);

        builder.Property(g => g.CreatedAt).IsRequired();
        builder.Property(g => g.UpdatedAt).IsRequired();
        builder.Property(g => g.DeletedAt);

        builder.Property(g => g.SpaceId).IsRequired();

        builder.HasMany(g => g.Scopes)
            .WithOne()
            .HasForeignKey(s => s.GoalId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(Goal.Scopes))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(g => new { g.UserId, g.Year });
        builder.HasIndex(g => new { g.UserId, g.Status });
        builder.HasIndex(g => new { g.UserId, g.SpaceId });
        builder.HasIndex(g => new { g.UserId, g.Kind });

        builder.HasQueryFilter(g => g.DeletedAt == null);
    }
}
