using System;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestindoEmNegocio.Migrations
{
    [DbContext(typeof(InvestDbContext))]
    [Migration("20260120140000_AddInvestmentsOnboarding")]
    public partial class AddInvestmentsOnboarding : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "investment_goals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investment_goals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "investment_positions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Asset = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    AvgPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OpenedAt = table.Column<DateOnly>(type: "date", nullable: false),
                    Account = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Note = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investment_positions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_onboarding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Step = table.Column<int>(type: "integer", nullable: false),
                    Completed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_onboarding", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "investment_movements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investment_movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_investment_movements_investment_positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "investment_positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_investment_goals_UserId",
                table: "investment_goals",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_investment_positions_UserId_Asset",
                table: "investment_positions",
                columns: new[] { "UserId", "Asset" });

            migrationBuilder.CreateIndex(
                name: "IX_investment_movements_PositionId",
                table: "investment_movements",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_user_onboarding_UserId",
                table: "user_onboarding",
                column: "UserId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "investment_movements");

            migrationBuilder.DropTable(
                name: "investment_goals");

            migrationBuilder.DropTable(
                name: "user_onboarding");

            migrationBuilder.DropTable(
                name: "investment_positions");
        }
    }
}
