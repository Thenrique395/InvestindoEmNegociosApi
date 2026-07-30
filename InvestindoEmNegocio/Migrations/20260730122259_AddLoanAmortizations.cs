using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestindoEmNegocio.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanAmortizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "loan_amortizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Strategy = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PreviousBalance = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    NewBalance = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    PreviousTerm = table.Column<int>(type: "integer", nullable: false),
                    NewTerm = table.Column<int>(type: "integer", nullable: false),
                    PreviousPayment = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    NewPayment = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    EstimatedInterestBefore = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    EstimatedInterestAfter = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    EstimatedSavings = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    ScheduleVersion = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_loan_amortizations", x => x.Id);
                    table.CheckConstraint("ck_loan_amortization_amount_positive", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_loan_amortizations_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_loan_amortizations_loan_contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "loan_contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_loan_amortizations_AccountId",
                table: "loan_amortizations",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_loan_amortizations_ContractId",
                table: "loan_amortizations",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_loan_amortizations_UserId_CreatedAt",
                table: "loan_amortizations",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_loan_amortizations_UserId_IdempotencyKey",
                table: "loan_amortizations",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "loan_amortizations");
        }
    }
}
