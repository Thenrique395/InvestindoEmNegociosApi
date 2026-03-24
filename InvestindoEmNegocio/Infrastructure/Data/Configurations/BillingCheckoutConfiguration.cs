using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public sealed class BillingCheckoutConfiguration : IEntityTypeConfiguration<BillingCheckout>
{
    public void Configure(EntityTypeBuilder<BillingCheckout> builder)
    {
        builder.ToTable("billing_checkouts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Provider).HasMaxLength(32).IsRequired();
        builder.Property(x => x.PlanCode).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(14, 2);
        builder.Property(x => x.ProviderCheckoutId).HasMaxLength(120);
        builder.Property(x => x.ProviderCustomerId).HasMaxLength(120);
        builder.Property(x => x.ProviderSubscriptionId).HasMaxLength(120);
        builder.Property(x => x.ProviderPaymentIntentId).HasMaxLength(120);
        builder.Property(x => x.ProviderPaymentStatus).HasMaxLength(60);
        builder.Property(x => x.LastProviderEventType).HasMaxLength(120);
        builder.Property(x => x.FailureReason).HasMaxLength(500);
        builder.Property(x => x.CheckoutUrl).HasMaxLength(1000);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.ProviderCheckoutId).IsUnique();
        builder.HasIndex(x => x.ProviderSubscriptionId);
        builder.HasIndex(x => new { x.UserId, x.Status, x.CreatedAt });
    }
}
