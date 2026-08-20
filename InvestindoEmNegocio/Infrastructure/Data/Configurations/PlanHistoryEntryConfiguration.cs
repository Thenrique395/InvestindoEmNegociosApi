using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class PlanHistoryEntryConfiguration : IEntityTypeConfiguration<PlanHistoryEntry>
{
    public void Configure(EntityTypeBuilder<PlanHistoryEntry> builder)
    {
        builder.ToTable("plan_history_entries");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId).IsRequired();
        builder.Property(e => e.SpaceId).IsRequired();
        builder.Property(e => e.PlanId).IsRequired();
        builder.Property(e => e.InstallmentId);

        // String, não int: o histórico é lido por gente, e um número no banco não
        // diz nada em consulta manual.
        builder.Property(e => e.Type)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.ActorUserId);

        builder.Property(e => e.OldValue).HasMaxLength(200);
        builder.Property(e => e.NewValue).HasMaxLength(200);
        builder.Property(e => e.CreatedAt).IsRequired();

        // A consulta do histórico é sempre "todos os eventos deste lançamento,
        // do mais antigo para o mais novo".
        builder.HasIndex(e => new { e.PlanId, e.OccurredAt });
    }
}
