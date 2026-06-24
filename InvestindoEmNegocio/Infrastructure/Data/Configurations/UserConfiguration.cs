using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(u => u.Document)
            .IsRequired()
            .HasMaxLength(11)
            .HasDefaultValue(string.Empty);

        builder.Property(u => u.AvatarUrl)
            .IsRequired(false)
            .HasMaxLength(400);

        builder.Property(u => u.City)
            .IsRequired(false)
            .HasMaxLength(120);

        builder.Property(u => u.State)
            .IsRequired(false)
            .HasMaxLength(80);

        builder.Property(u => u.Country)
            .IsRequired(false)
            .HasMaxLength(120);

        builder.Property(u => u.Phone)
            .IsRequired()
            .HasMaxLength(30)
            .HasDefaultValue(string.Empty);

        builder.Property(u => u.BirthDate);

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(UserRole.Basic);

        builder.Property(u => u.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        builder.Property(u => u.UpdatedAt)
            .IsRequired();

        builder.Property(u => u.LastLoginAt);

        builder.Property(u => u.FailedLoginAttempts)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(u => u.LockoutUntil);
        builder.Property(u => u.TrialUsedAt);

        builder.Property(u => u.IsAnonymized)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.DeletedAt);

        builder.Property(u => u.TokenVersion)
            .IsRequired()
            .HasDefaultValue(0);

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.HasIndex(u => u.Document)
            .IsUnique()
            .HasFilter("\"Document\" <> ''");
    }
}
