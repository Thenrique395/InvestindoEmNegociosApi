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

        builder.Property(x => x.FinancialGoal)
            .IsRequired(false)
            .HasMaxLength(80);
        builder.Property(x => x.CarryOverDay)
            .IsRequired()
            .HasDefaultValue(1);
        builder.Property(x => x.IntelligenceMode)
            .IsRequired()
            .HasMaxLength(1)
            .HasDefaultValue("B");
        builder.Property(x => x.Language)
            .IsRequired(false)
            .HasMaxLength(10);
        builder.Property(x => x.Currency)
            .IsRequired(false)
            .HasMaxLength(10);
        builder.Property(x => x.NotifyUpcomingEnabled).IsRequired();
        builder.Property(x => x.NotifyOverdueEnabled).IsRequired();
        builder.Property(x => x.NotifyEmailEnabled).IsRequired();
        builder.Property(x => x.NotifyInAppEnabled).IsRequired();
        builder.Property(x => x.NotifyDaysBeforeDue).IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.UserId).IsUnique();
    }
}
