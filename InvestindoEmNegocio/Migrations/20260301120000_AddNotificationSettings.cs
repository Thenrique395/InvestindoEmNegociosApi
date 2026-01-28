using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestindoEmNegocio.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferenceKey",
                table: "user_notifications",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE user_notifications
                SET "ReferenceKey" = CONCAT('legacy:', "Id")
                WHERE "ReferenceKey" = '';
                """);

            migrationBuilder.DropIndex(
                name: "IX_user_notifications_UserId_InstallmentId_Kind_DueDate",
                table: "user_notifications");

            migrationBuilder.CreateTable(
                name: "notification_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IncomeUpcomingEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IncomeDaysBefore = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                    ExpenseUpcomingEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ExpenseDaysBefore = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                    ExpenseOverdueEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CardCloseSoonEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CardCloseDaysBefore = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                    CardCloseDayEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    MonthCloseEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    MonthSummaryEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    GoalBelowExpectedEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    GoalCompletedEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    GoalInactivityEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    GoalInactivityDays = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_settings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_notifications_UserId_ReferenceKey",
                table: "user_notifications",
                columns: new[] { "UserId", "ReferenceKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_settings");

            migrationBuilder.DropIndex(
                name: "IX_user_notifications_UserId_ReferenceKey",
                table: "user_notifications");

            migrationBuilder.CreateIndex(
                name: "IX_user_notifications_UserId_InstallmentId_Kind_DueDate",
                table: "user_notifications",
                columns: new[] { "UserId", "InstallmentId", "Kind", "DueDate" });

            migrationBuilder.DropColumn(
                name: "ReferenceKey",
                table: "user_notifications");
        }
    }
}
