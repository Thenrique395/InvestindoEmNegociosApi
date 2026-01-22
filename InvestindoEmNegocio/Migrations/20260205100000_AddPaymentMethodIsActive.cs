using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestindoEmNegocio.Migrations;

[DbContext(typeof(InvestDbContext))]
[Migration("20260205100000_AddPaymentMethodIsActive")]
public partial class AddPaymentMethodIsActive : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsActive",
            table: "payment_methods",
            type: "boolean",
            nullable: false,
            defaultValue: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsActive",
            table: "payment_methods");
    }
}
