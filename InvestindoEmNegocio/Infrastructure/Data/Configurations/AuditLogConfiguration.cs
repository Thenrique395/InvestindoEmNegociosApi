using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.UserId);

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(80);

        builder.Property(a => a.Entity)
            .IsRequired()
            .HasMaxLength(80);

        builder.Property(a => a.EntityId)
            .HasMaxLength(200);

        builder.Property(a => a.IpAddress)
            .HasMaxLength(64);

        builder.Property(a => a.UserAgent)
            .HasMaxLength(400);

        builder.Property(a => a.Metadata)
            .HasColumnType("text");

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.CreatedAt);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
