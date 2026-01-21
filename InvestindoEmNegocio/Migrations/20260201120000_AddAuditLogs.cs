using System;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestindoEmNegocio.Migrations;

[DbContext(typeof(InvestDbContext))]
[Migration("20260201120000_AddAuditLogs")]
public partial class AddAuditLogs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "audit_logs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: true),
                Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Entity = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                EntityId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                UserAgent = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                Metadata = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_audit_logs", x => x.Id);
                table.ForeignKey(
                    name: "FK_audit_logs_users_UserId",
                    column: x => x.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_audit_logs_CreatedAt",
            table: "audit_logs",
            column: "CreatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_audit_logs_UserId",
            table: "audit_logs",
            column: "UserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "audit_logs");
    }
}
