using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestindoEmNegocio.Migrations
{
    /// <inheritdoc />
    public partial class ExpandLoanContractAndInstallment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "loan_installments",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FeeAmount",
                table: "loan_installments",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "InsuranceAmount",
                table: "loan_installments",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "loan_installments",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PenaltyAmount",
                table: "loan_installments",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RemainingAmount",
                table: "loan_installments",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ScheduleVersion",
                table: "loan_installments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "loan_contracts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AssetAmount",
                table: "loan_contracts",
                type: "numeric(14,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CetRate",
                table: "loan_contracts",
                type: "numeric(9,6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "loan_contracts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractType",
                table: "loan_contracts",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "loan_contracts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DownPaymentAmount",
                table: "loan_contracts",
                type: "numeric(14,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EffectiveAnnualRate",
                table: "loan_contracts",
                type: "numeric(9,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinancedAmount",
                table: "loan_contracts",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "GracePeriodMonths",
                table: "loan_contracts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "InstitutionId",
                table: "loan_contracts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstitutionName",
                table: "loan_contracts",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InterestRatePeriod",
                table: "loan_contracts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyInterestRate",
                table: "loan_contracts",
                type: "numeric(9,6)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OpenBalance",
                table: "loan_contracts",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "OriginalTermMonths",
                table: "loan_contracts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "loan_contracts",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidInterest",
                table: "loan_contracts",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidPrincipal",
                table: "loan_contracts",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "SpaceId",
                table: "loan_contracts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "loan_contracts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_loan_contracts_SpaceId_Status",
                table: "loan_contracts",
                columns: new[] { "SpaceId", "Status" });

            // ----------------------------------------------------------------
            // Backfill dos contratos existentes (preserva dados; idempotente por WHERE).
            // Enums persistidos como string NÃO podem ficar "" (quebra a leitura no EF).
            // Específico do Postgres (sintaxe UPDATE ... FROM). Nos testes SQLite não há
            // dados legados para backfill — pula com segurança.
            // ----------------------------------------------------------------
            if (migrationBuilder.ActiveProvider?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
            {
                migrationBuilder.Sql(@"
                UPDATE loan_contracts SET ""ContractType"" = 'Other'
                    WHERE ""ContractType"" IS NULL OR ""ContractType"" = '';
                UPDATE loan_contracts SET ""InterestRatePeriod"" = 'AnnualNominal'
                    WHERE ""InterestRatePeriod"" IS NULL OR ""InterestRatePeriod"" = '';
                UPDATE loan_contracts SET ""FinancedAmount"" = ""PrincipalAmount""
                    WHERE ""FinancedAmount"" = 0;
                UPDATE loan_contracts SET ""OriginalTermMonths"" = ""TermMonths""
                    WHERE ""OriginalTermMonths"" = 0;
                UPDATE loan_contracts SET ""MonthlyInterestRate"" = ROUND(""AnnualInterestRate"" / 1200.0, 6)
                    WHERE ""MonthlyInterestRate"" = 0;
                UPDATE loan_installments SET
                    ""RemainingAmount"" = CASE WHEN ""Status"" = 'Paid' THEN 0 ELSE ""TotalAmount"" END,
                    ""PaidAmount""      = CASE WHEN ""Status"" = 'Paid' THEN ""TotalAmount"" ELSE 0 END
                    WHERE ""RemainingAmount"" = 0;
                UPDATE loan_contracts c SET ""OpenBalance"" = COALESCE(
                        (SELECT SUM(li.""TotalAmount"") FROM loan_installments li
                         WHERE li.""ContractId"" = c.""Id"" AND li.""Status"" = 'Open'), 0)
                    WHERE c.""OpenBalance"" = 0;
                UPDATE loan_contracts c SET ""SpaceId"" = s.""Id""
                    FROM spaces s
                    WHERE s.""UserId"" = c.""UserId"" AND s.""IsDefault"" = TRUE AND s.""DeletedAt"" IS NULL
                      AND c.""SpaceId"" = '00000000-0000-0000-0000-000000000000';
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_loan_contracts_SpaceId_Status",
                table: "loan_contracts");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "loan_installments");

            migrationBuilder.DropColumn(
                name: "FeeAmount",
                table: "loan_installments");

            migrationBuilder.DropColumn(
                name: "InsuranceAmount",
                table: "loan_installments");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "loan_installments");

            migrationBuilder.DropColumn(
                name: "PenaltyAmount",
                table: "loan_installments");

            migrationBuilder.DropColumn(
                name: "RemainingAmount",
                table: "loan_installments");

            migrationBuilder.DropColumn(
                name: "ScheduleVersion",
                table: "loan_installments");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "loan_contracts");

            migrationBuilder.DropColumn(
                name: "AssetAmount",
                table: "loan_contracts");

            migrationBuilder.DropColumn(
                name: "CetRate",
                table: "loan_contracts");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "loan_contracts");

            migrationBuilder.DropColumn(
                name: "ContractType",
                table: "loan_contracts");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "loan_contracts");

            migrationBuilder.DropColumn(
                name: "DownPaymentAmount",
                table: "loan_contracts");

            migrationBuilder.DropColumn(
                name: "EffectiveAnnualRate",
                table: "loan_contracts");

            migrationBuilder.DropColumn(
                name: "FinancedAmount",
                table: "loan_contracts");

            migrationBuilder.DropColumn(
                name: "GracePeriodMonths",
                table: "loan_contracts");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                table: "loan_contracts");

            migrationBuilder.DropColumn(
                name: "InstitutionName",
                table: "loan_contracts");

            migrationBuilder.DropColumn(
                name: "InterestRatePeriod",
                table: "loan_contracts");

            migrationBuilder.DropColumn(
                name: "MonthlyInterestRate",
                table: "loan_contracts");

            migrationBuilder.DropColumn(
                name: "OpenBalance",
                table: "loan_contracts");

            migrationBuilder.DropColumn(
                name: "OriginalTermMonths",
                table: "loan_contracts");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "loan_contracts");

            migrationBuilder.DropColumn(
                name: "PaidInterest",
                table: "loan_contracts");

            migrationBuilder.DropColumn(
                name: "PaidPrincipal",
                table: "loan_contracts");

            migrationBuilder.DropColumn(
                name: "SpaceId",
                table: "loan_contracts");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "loan_contracts");
        }
    }
}
