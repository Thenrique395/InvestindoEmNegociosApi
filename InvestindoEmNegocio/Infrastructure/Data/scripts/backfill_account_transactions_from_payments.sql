-- Backfill de ledger por pagamentos já registrados.
-- Objetivo:
-- 1) Remover movimentações órfãs (source aponta para pagamento inexistente)
-- 2) Inserir movimentações faltantes para pagamentos com AccountId preenchido
--
-- Seguro para reexecução (idempotente por SourceType+SourceId).

BEGIN;

-- 1) Limpa órfãos do ledger gerados por pagamentos removidos.
DELETE FROM account_transactions t
WHERE t."SourceType" = 'InstallmentPayment'
  AND t."SourceId" IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM money_payments p
      WHERE p."Id" = t."SourceId"
  );

-- 2) Cria movimentações faltantes para pagamentos válidos com conta associada.
INSERT INTO account_transactions (
    "Id",
    "AccountId",
    "UserId",
    "OccurredAt",
    "Kind",
    "Amount",
    "Description",
    "SourceType",
    "SourceId",
    "CreatedAt"
)
SELECT
    p."Id" AS "Id",
    p."AccountId",
    p."UserId",
    p."PaidAt" AS "OccurredAt",
    CASE WHEN mp."Type" = 'Income' THEN 'Credit' ELSE 'Debit' END AS "Kind",
    p."PaidAmount" AS "Amount",
    ('Pagamento parcela ' || mi."InstallmentNo" || ' - ' || mp."Title") AS "Description",
    'InstallmentPayment' AS "SourceType",
    p."Id" AS "SourceId",
    p."CreatedAt"
FROM money_payments p
JOIN money_installments mi ON mi."Id" = p."InstallmentId"
JOIN money_plans mp ON mp."Id" = mi."PlanId"
WHERE p."AccountId" IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM account_transactions t
      WHERE t."SourceType" = 'InstallmentPayment'
        AND t."SourceId" = p."Id"
  );

COMMIT;
