using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestindoEmNegocio.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NotifyEmailEnabled",
                table: "user_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyInAppEnabled",
                table: "user_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyOverdueEnabled",
                table: "user_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyUpcomingEnabled",
                table: "user_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "NotifyDaysBeforeDue",
                table: "user_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.CreateTable(
                name: "user_notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    InstallmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    MoneyType = table.Column<string>(type: "text", nullable: true),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_notifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_notifications_UserId_CreatedAt",
                table: "user_notifications",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_user_notifications_UserId_InstallmentId_Kind_DueDate",
                table: "user_notifications",
                columns: new[] { "UserId", "InstallmentId", "Kind", "DueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_notifications");

            migrationBuilder.DropColumn(
                name: "NotifyEmailEnabled",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "NotifyInAppEnabled",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "NotifyOverdueEnabled",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "NotifyUpcomingEnabled",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "NotifyDaysBeforeDue",
                table: "user_profiles");
        }
    }
}
