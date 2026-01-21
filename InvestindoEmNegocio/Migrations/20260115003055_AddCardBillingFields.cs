using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestindoEmNegocio.Migrations;

public partial class AddCardBillingFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Bank",
            table: "cards",
            type: "character varying(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "CreditLimit",
            table: "cards",
            type: "numeric(14,2)",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<int>(
            name: "StatementCloseDay",
            table: "cards",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "DueDay",
            table: "cards",
            type: "integer",
            nullable: false,
            defaultValue: 1);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Bank",
            table: "cards");

        migrationBuilder.DropColumn(
            name: "CreditLimit",
            table: "cards");

        migrationBuilder.DropColumn(
            name: "StatementCloseDay",
            table: "cards");

        migrationBuilder.DropColumn(
            name: "DueDay",
            table: "cards");
    }
}
