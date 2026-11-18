using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestindoEmNegocio.Migrations
{
    /// <inheritdoc />
    public partial class IsolateBudgetsAndSnapshotsBySpace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_monthly_financial_snapshots_UserId_Year_Month",
                table: "monthly_financial_snapshots");

            migrationBuilder.DropIndex(
                name: "IX_monthly_budget_items_UserId_Year_Month",
                table: "monthly_budget_items");

            migrationBuilder.DropIndex(
                name: "IX_monthly_budget_items_UserId_Year_Month_CategoryName",
                table: "monthly_budget_items");

            migrationBuilder.AddColumn<Guid>(
                name: "SpaceId",
                table: "monthly_financial_snapshots",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SpaceId",
                table: "monthly_budget_items",
                type: "uuid",
                nullable: true);

            // Registros globais anteriores são preservados no espaço padrão, nunca duplicados.
            migrationBuilder.Sql("""
                INSERT INTO spaces ("Id", "UserId", "Name", "IsDefault", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), owners."UserId", 'Pessoal', TRUE, NOW(), NOW()
                FROM (SELECT "UserId" FROM monthly_budget_items UNION SELECT "UserId" FROM monthly_financial_snapshots) owners
                WHERE NOT EXISTS (SELECT 1 FROM spaces s WHERE s."UserId" = owners."UserId" AND s."IsDefault" AND s."DeletedAt" IS NULL);
                UPDATE monthly_budget_items b SET "SpaceId" = (
                    SELECT s."Id" FROM spaces s WHERE s."UserId" = b."UserId" AND s."IsDefault" AND s."DeletedAt" IS NULL);
                UPDATE monthly_financial_snapshots b SET "SpaceId" = (
                    SELECT s."Id" FROM spaces s WHERE s."UserId" = b."UserId" AND s."IsDefault" AND s."DeletedAt" IS NULL);
                """);
            migrationBuilder.AlterColumn<Guid>(name: "SpaceId", table: "monthly_budget_items", type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "SpaceId", table: "monthly_financial_snapshots", type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_monthly_financial_snapshots_UserId_SpaceId_Year_Month",
                table: "monthly_financial_snapshots",
                columns: new[] { "UserId", "SpaceId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_monthly_budget_items_UserId_SpaceId_Year_Month",
                table: "monthly_budget_items",
                columns: new[] { "UserId", "SpaceId", "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_monthly_budget_items_UserId_SpaceId_Year_Month_CategoryName",
                table: "monthly_budget_items",
                columns: new[] { "UserId", "SpaceId", "Year", "Month", "CategoryName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM monthly_budget_items GROUP BY "UserId", "Year", "Month", "CategoryName" HAVING COUNT(*) > 1)
                    OR EXISTS (SELECT 1 FROM monthly_financial_snapshots GROUP BY "UserId", "Year", "Month" HAVING COUNT(*) > 1) THEN
                        RAISE EXCEPTION 'Reversão bloqueada: existem dados de espaços distintos. Consolide-os explicitamente antes de reverter.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_monthly_financial_snapshots_UserId_SpaceId_Year_Month",
                table: "monthly_financial_snapshots");

            migrationBuilder.DropIndex(
                name: "IX_monthly_budget_items_UserId_SpaceId_Year_Month",
                table: "monthly_budget_items");

            migrationBuilder.DropIndex(
                name: "IX_monthly_budget_items_UserId_SpaceId_Year_Month_CategoryName",
                table: "monthly_budget_items");

            migrationBuilder.DropColumn(
                name: "SpaceId",
                table: "monthly_financial_snapshots");

            migrationBuilder.DropColumn(
                name: "SpaceId",
                table: "monthly_budget_items");

            migrationBuilder.CreateIndex(
                name: "IX_monthly_financial_snapshots_UserId_Year_Month",
                table: "monthly_financial_snapshots",
                columns: new[] { "UserId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_monthly_budget_items_UserId_Year_Month",
                table: "monthly_budget_items",
                columns: new[] { "UserId", "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_monthly_budget_items_UserId_Year_Month_CategoryName",
                table: "monthly_budget_items",
                columns: new[] { "UserId", "Year", "Month", "CategoryName" },
                unique: true);
        }
    }
}
