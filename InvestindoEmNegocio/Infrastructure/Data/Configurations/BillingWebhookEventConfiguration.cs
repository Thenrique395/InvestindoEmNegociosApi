using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public sealed class BillingWebhookEventConfiguration : IEntityTypeConfiguration<BillingWebhookEvent>
{
    public void Configure(EntityTypeBuilder<BillingWebhookEvent> builder)
    {
        builder.ToTable("billing_webhook_events");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Provider).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ProviderEventId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(120).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnType("text").IsRequired();
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);

        builder.HasIndex(x => new { x.Provider, x.ProviderEventId }).IsUnique();
        builder.HasIndex(x => x.ReceivedAt);
    }
}
