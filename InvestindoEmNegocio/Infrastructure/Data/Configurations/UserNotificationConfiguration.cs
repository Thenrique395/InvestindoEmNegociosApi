using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.ToTable("user_notifications");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(160);

        builder.Property(x => x.Message)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.ReferenceKey)
            .IsRequired()
            .HasMaxLength(160);

        builder.Property(x => x.MoneyType)
            .HasConversion<string?>();
        builder.Property(x => x.Kind)
            .HasConversion<string>()
            .IsRequired();
        builder.Property(x => x.DueDate);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ReadAt);

        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
        builder.HasIndex(x => new { x.UserId, x.ReferenceKey }).IsUnique();
    }
}
