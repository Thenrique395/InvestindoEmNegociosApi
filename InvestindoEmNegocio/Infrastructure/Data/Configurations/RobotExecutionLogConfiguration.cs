using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public sealed class RobotExecutionLogConfiguration : IEntityTypeConfiguration<RobotExecutionLog>
{
    public void Configure(EntityTypeBuilder<RobotExecutionLog> builder)
    {
        builder.ToTable("robot_execution_logs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RobotName)
            .IsRequired()
            .HasMaxLength(80);

        builder.Property(x => x.StartedAt)
            .IsRequired();

        builder.Property(x => x.FinishedAt)
            .IsRequired();

        builder.Property(x => x.DurationMs)
            .IsRequired();

        builder.Property(x => x.CorrelationId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.HostName)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(x => x.TriggeredByUserId);

        builder.Property(x => x.Success)
            .IsRequired();

        builder.Property(x => x.ProcessedCount)
            .IsRequired();

        builder.Property(x => x.EmailsAttempted)
            .IsRequired();

        builder.Property(x => x.EmailsSent)
            .IsRequired();

        builder.Property(x => x.EmailsFailed)
            .IsRequired();

        builder.Property(x => x.ZeroItemsReasonCode)
            .HasMaxLength(100);

        builder.Property(x => x.WasSkipped)
            .IsRequired();

        builder.Property(x => x.SkipReason)
            .HasMaxLength(200);

        builder.Property(x => x.Error)
            .HasMaxLength(2000);

        builder.HasIndex(x => new { x.RobotName, x.StartedAt });
    }
}
