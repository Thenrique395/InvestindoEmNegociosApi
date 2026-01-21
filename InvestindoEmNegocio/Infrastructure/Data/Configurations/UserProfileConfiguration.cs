using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("user_profiles");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FullName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Document)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Phone)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(x => x.BirthDate);
        builder.Property(x => x.AvatarUrl)
            .IsRequired(false)
            .HasMaxLength(400);
        builder.Property(x => x.City)
            .IsRequired(false)
            .HasMaxLength(120);
        builder.Property(x => x.State)
            .IsRequired(false)
            .HasMaxLength(80);
        builder.Property(x => x.Country)
            .IsRequired(false)
            .HasMaxLength(120);
        builder.Property(x => x.Language)
            .IsRequired(false)
            .HasMaxLength(10);
        builder.Property(x => x.Currency)
            .IsRequired(false)
            .HasMaxLength(10);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.UserId).IsUnique();
    }
}
