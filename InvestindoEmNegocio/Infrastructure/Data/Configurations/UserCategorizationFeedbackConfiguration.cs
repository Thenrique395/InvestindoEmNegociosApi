using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public sealed class UserCategorizationFeedbackConfiguration : IEntityTypeConfiguration<UserCategorizationFeedback>
{
    public void Configure(EntityTypeBuilder<UserCategorizationFeedback> builder)
    {
        builder.ToTable("user_categorization_feedback");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.NormalizedPattern)
            .IsRequired()
            .HasMaxLength(160);

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.Hits)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(x => x.FirstLearnedAt).IsRequired();
        builder.Property(x => x.LastLearnedAt).IsRequired();

        builder.HasIndex(x => new { x.UserId, x.Type, x.NormalizedPattern }).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.Type, x.LastLearnedAt });
    }
}
