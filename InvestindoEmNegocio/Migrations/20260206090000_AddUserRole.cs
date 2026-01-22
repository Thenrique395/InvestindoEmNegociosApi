using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestindoEmNegocio.Migrations;

[DbContext(typeof(InvestDbContext))]
[Migration("20260206090000_AddUserRole")]
public partial class AddUserRole : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Role",
            table: "users",
            type: "text",
            nullable: false,
            defaultValue: "Basic");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Role",
            table: "users");
    }
}
