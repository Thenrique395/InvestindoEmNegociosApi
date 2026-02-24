# Playbook - Fluxo de saldo por transações (base limpa)

## Objetivo

Validar rapidamente o fluxo operacional do ledger (`account_transactions`) após limpar base e manter somente tabelas de configuração.

## Pré-condições

- API rodando com migrations aplicadas.
- Usuário de teste autenticável.
- Base sem dados de domínio (usuários/planos/parcelas/pagamentos/contas podem estar vazios).

## Fluxo mínimo (manual)

1. Criar usuário novo (perfil `Basic`).
2. Confirmar que a conta padrão foi criada automaticamente.
3. Criar um plano de despesa.
4. Pagar uma parcela do plano (gera débito em `account_transactions`).
5. Estornar o pagamento pela nova rota:
   - `POST /api/v1/installments/{installmentId}/payments/{paymentId}/reversals`
6. Validar que:
   - existe um `MoneyPayment` negativo para o estorno;
   - existe transação de estorno no ledger com `SourceType = InstallmentPaymentReversal`;
   - status da parcela foi recalculado (`OPEN`, `PARTIALLY_PAID` ou `PAID` conforme soma líquida).

## Consultas SQL úteis

```sql
-- pagamentos (positivos e estornos negativos)
select id, installment_id, paid_at, paid_amount, account_id, note
from money_payments
order by created_at desc;

-- ledger
select id, account_id, occurred_at, kind, amount, source_type, source_id, description
from account_transactions
order by created_at desc;

-- saldo por conta
select
  a.id,
  a.name,
  a.initial_balance,
  coalesce(sum(case when t.kind = 1 then t.amount else -t.amount end), 0) as net_transactions,
  a.initial_balance + coalesce(sum(case when t.kind = 1 then t.amount else -t.amount end), 0) as current_balance
from accounts a
left join account_transactions t on t.account_id = a.id
group by a.id, a.name, a.initial_balance
order by a.name;
```

## Regras atuais importantes

- Usuário `Basic` sempre usa conta padrão automática.
- Usuário `Intermediate`/`Advanced`:
  - com 1 conta ativa: usa automaticamente;
  - com múltiplas contas ativas: `AccountId` no pagamento é obrigatório.
- Estorno duplicado do mesmo pagamento é bloqueado.

## Troubleshooting rápido

- Erro `Conta obrigatória`: usuário sem conta ativa ou sem `AccountId` em cenário multi-conta.
- Erro `Pagamento já estornado`: já existe transação de estorno para o `paymentId`.
- Saldo divergente: conferir se o pagamento foi criado com conta e se o estorno também gerou lançamento no ledger.
