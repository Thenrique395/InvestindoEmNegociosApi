using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class UserOnboardingConfiguration : IEntityTypeConfiguration<UserOnboarding>
{
    public void Configure(EntityTypeBuilder<UserOnboarding> builder)
    {
        builder.ToTable("user_onboarding");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.Step).IsRequired();
        builder.Property(x => x.Completed).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.UserId).IsUnique();
    }
}
