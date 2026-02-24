using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public sealed class RobotSettingsConfiguration : IEntityTypeConfiguration<RobotSettings>
{
    public void Configure(EntityTypeBuilder<RobotSettings> builder)
    {
        builder.ToTable("robot_settings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Enabled).IsRequired();
        builder.Property(x => x.DailyRunTimeUtc)
            .IsRequired()
            .HasMaxLength(5);
        builder.Property(x => x.UpdatedAt).IsRequired();
    }
}
