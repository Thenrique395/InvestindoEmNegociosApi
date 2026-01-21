using System;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestindoEmNegocio.Migrations;

[DbContext(typeof(InvestDbContext))]
[Migration("20260115013000_AddCardIdToMoneyPlans")]
public partial class AddCardIdToMoneyPlans : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "CardId",
            table: "money_plans",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddForeignKey(
            name: "FK_money_plans_cards_CardId",
            table: "money_plans",
            column: "CardId",
            principalTable: "cards",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_money_plans_cards_CardId",
            table: "money_plans");

        migrationBuilder.DropColumn(
            name: "CardId",
            table: "money_plans");
    }
}
