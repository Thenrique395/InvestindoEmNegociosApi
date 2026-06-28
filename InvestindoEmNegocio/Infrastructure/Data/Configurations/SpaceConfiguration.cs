using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class SpaceConfiguration : IEntityTypeConfiguration<Space>
{
    public void Configure(EntityTypeBuilder<Space> builder)
    {
        builder.ToTable("spaces");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(s => s.PasswordHash)
            .HasMaxLength(200);

        builder.Property(s => s.IsDefault)
            .IsRequired();

        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();
        builder.Property(s => s.DeletedAt);

        builder.HasIndex(s => new { s.UserId, s.IsDefault });

        builder.HasQueryFilter(s => s.DeletedAt == null);
    }
}
