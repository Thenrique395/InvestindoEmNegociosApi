using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestindoEmNegocio.Migrations
{
    /// <summary>
    /// Estorno de pagamento é gravado como <c>MoneyPayment</c> NEGATIVO espelhando o original —
    /// é assim que o status é rederivado (soma líquida) e que a API marca <c>isReversal</c>.
    ///
    /// A constraint <c>ck_payment_amount_positive</c> exigia <c>PaidAmount &gt; 0</c> e derrubava
    /// todo estorno com CHECK violation, que virava 500. O endpoint nunca funcionou desde que
    /// ela entrou. Passa a exigir apenas que o valor não seja ZERO.
    ///
    /// O EF não gera alteração de check constraint sozinho, por isso o SQL é explícito.
    /// </summary>
    public partial class PermitirEstornoComoPagamentoNegativo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Só Postgres: SQLite não suporta ALTER TABLE ... ADD CONSTRAINT, e os testes de
            // migration rodam em SQLite. Lá a constraint já nasce certa pelo EnsureCreated,
            // a partir da configuração do modelo.
            if (!migrationBuilder.ActiveProvider!.Contains("Npgsql")) return;

            migrationBuilder.Sql(
                "ALTER TABLE money_payments DROP CONSTRAINT IF EXISTS ck_payment_amount_positive;");
            migrationBuilder.Sql(
                "ALTER TABLE money_payments DROP CONSTRAINT IF EXISTS ck_payment_amount_nonzero;");
            migrationBuilder.Sql(
                "ALTER TABLE money_payments ADD CONSTRAINT ck_payment_amount_nonzero CHECK (\"PaidAmount\" <> 0);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (!migrationBuilder.ActiveProvider!.Contains("Npgsql")) return;

            migrationBuilder.Sql(
                "ALTER TABLE money_payments DROP CONSTRAINT IF EXISTS ck_payment_amount_nonzero;");
            migrationBuilder.Sql(
                "ALTER TABLE money_payments ADD CONSTRAINT ck_payment_amount_positive CHECK (\"PaidAmount\" > 0);");
        }
    }
}
