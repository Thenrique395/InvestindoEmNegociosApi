using System;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestindoEmNegocio.Migrations;

[DbContext(typeof(InvestDbContext))]
[Migration("20260201090000_AddUserLockoutFields")]
public partial class AddUserLockoutFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "FailedLoginAttempts",
            table: "users",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name: "LockoutUntil",
            table: "users",
            type: "timestamp with time zone",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "FailedLoginAttempts",
            table: "users");

        migrationBuilder.DropColumn(
            name: "LockoutUntil",
            table: "users");
    }
}
