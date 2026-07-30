using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestindoEmNegocio.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "loan_payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstallmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    PrincipalAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    InterestAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    PenaltyAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    MethodId = table.Column<int>(type: "integer", nullable: true),
                    AccountTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceiptUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReversedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReversalReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_payments", x => x.Id);
                    table.CheckConstraint("ck_loan_payment_amount_positive", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_loan_payments_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_loan_payments_loan_contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "loan_contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_loan_payments_loan_installments_InstallmentId",
                        column: x => x.InstallmentId,
                        principalTable: "loan_installments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_loan_payments_AccountId",
                table: "loan_payments",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_loan_payments_ContractId",
                table: "loan_payments",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_loan_payments_InstallmentId",
                table: "loan_payments",
                column: "InstallmentId");

            migrationBuilder.CreateIndex(
                name: "IX_loan_payments_UserId_IdempotencyKey",
                table: "loan_payments",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_loan_payments_UserId_PaidAt",
                table: "loan_payments",
                columns: new[] { "UserId", "PaidAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "loan_payments");
        }
    }
}
