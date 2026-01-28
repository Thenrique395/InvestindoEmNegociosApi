using InvestindoEmNegocio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestindoEmNegocio.Infrastructure.Data.Configurations;

public class NotificationSettingsConfiguration : IEntityTypeConfiguration<NotificationSettings>
{
    public void Configure(EntityTypeBuilder<NotificationSettings> builder)
    {
        builder.ToTable("notification_settings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.IncomeUpcomingEnabled).IsRequired();
        builder.Property(x => x.IncomeDaysBefore).IsRequired();
        builder.Property(x => x.ExpenseUpcomingEnabled).IsRequired();
        builder.Property(x => x.ExpenseDaysBefore).IsRequired();
        builder.Property(x => x.ExpenseOverdueEnabled).IsRequired();
        builder.Property(x => x.CardCloseSoonEnabled).IsRequired();
        builder.Property(x => x.CardCloseDaysBefore).IsRequired();
        builder.Property(x => x.CardCloseDayEnabled).IsRequired();
        builder.Property(x => x.MonthCloseEnabled).IsRequired();
        builder.Property(x => x.MonthSummaryEnabled).IsRequired();
        builder.Property(x => x.GoalBelowExpectedEnabled).IsRequired();
        builder.Property(x => x.GoalCompletedEnabled).IsRequired();
        builder.Property(x => x.GoalInactivityEnabled).IsRequired();
        builder.Property(x => x.GoalInactivityDays).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
    }
}
