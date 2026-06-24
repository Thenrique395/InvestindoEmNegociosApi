using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public sealed class UserSubscriptionConfiguration : IEntityTypeConfiguration<UserSubscription>
{
    public void Configure(EntityTypeBuilder<UserSubscription> builder)
    {
        builder.ToTable("user_subscriptions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PlanCode).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Provider).HasMaxLength(32).IsRequired();
        builder.Property(x => x.PriceAmount).HasPrecision(14, 2);
        builder.Property(x => x.ExternalCustomerId).HasMaxLength(120);
        builder.Property(x => x.ExternalSubscriptionId).HasMaxLength(120);
        builder.Property(x => x.ExternalPriceId).HasMaxLength(120);
        builder.Property(x => x.IsTrial).HasDefaultValue(false);

        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasIndex(x => x.ExternalSubscriptionId);
    }
}
