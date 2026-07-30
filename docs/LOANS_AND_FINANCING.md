# Empréstimos e Financiamentos

Documentação do módulo de empréstimos/financiamentos (backend `InvestindoEmNegociosApi` + frontend
`InvestindoEmNegociosWeb`). Reflete as Fases 1–6 já implementadas.

> Estado: implementado e **unit-testado** (backend 634 / frontend 530). **Ainda não validado em DEV**
> (3 migrations pendentes — ver seção Migrations). E2E e integrações cross-module dependem de deploy.

## Objetivo

Permitir simular, comparar (PRICE×SAC), criar e acompanhar contratos de empréstimo/financiamento:
saldo devedor, parcelas, pagamentos integrados a contas, amortização extraordinária, quitação,
arquivamento e histórico.

## Domínio / Entidades

- **`LoanContract`** — contrato. Campos principais: `UserId`, `SpaceId` (isolamento), `Title`,
  `ContractType`, `PrincipalAmount`/`FinancedAmount`, `AnnualInterestRate`/`MonthlyInterestRate`/
  `InterestRatePeriod`, `TermMonths`/`OriginalTermMonths`, `AmortizationType`, `MonthlyPayment`,
  `TotalCost`/`TotalInterest`, `OpenBalance`, `PaidAmount`/`PaidPrincipal`/`PaidInterest`, `Status`,
  `ClosedAt`/`ArchivedAt`, `Version` (concorrência otimista).
- **`LoanInstallment`** — parcela. `BeginningBalance`, `PrincipalAmount`, `InterestAmount`,
  encargos (`Insurance/Fee/Penalty/Discount`), `TotalAmount`, `EndingBalance`, `PaidAmount`/
  `RemainingAmount`, `Status`, `ScheduleVersion`, `Version`.
- **`LoanPayment`** — pagamento (histórico próprio, nunca apagado). Split principal/juros/multa/
  desconto, `AccountId`, `AccountTransactionId`, `ReceiptUrl`, `IdempotencyKey`, `ReversedAt`.
- **`LoanAmortization`** — amortização extraordinária. Estratégia, saldos/prazo/parcela antes/depois,
  economia estimada, `ScheduleVersion`, `IdempotencyKey`, `ReversedAt`.

### Enums

- `LoanContractType`: Mortgage, VehicleFinancing, PersonalLoan, PayrollLoan, BusinessLoan,
  Refinancing, CreditAgreement, Other.
- `LoanStatus`: Active, Closed (existentes) + Draft, Overdue, Cancelled, Archived, Renegotiated.
- `LoanInstallmentStatus`: Open, Paid (existentes) + Overdue, PartiallyPaid, Anticipated, Cancelled,
  Renegotiated.
- `InterestRatePeriod`: AnnualNominal (default, convenção atual), AnnualEffective, Monthly.
- `LoanAmortizationStrategy` (Domain.Finance): ReduceTerm, ReducePayment, FullSettlement.

## Cálculos (`Domain/Finance/LoanCalculator`)

Calculadora **pura e determinística** — fonte oficial dos cálculos.

- **Arredondamento**: half-up (`MidpointRounding.AwayFromZero`), 2 casas. A **última parcela absorve
  o resíduo** de centavos → saldo final fecha em 0,00; Σamortização = principal; Σparcelas = custo total.
- **Taxa**: opera sempre com a mensal explícita. Conversões: nominal (anual/12) e efetiva
  ((1+anual)^(1/12)−1). A API trata a taxa informada como **anual nominal** (default documentado).
- **PRICE**: `PMT = PV·i/(1−(1+i)^−n)`. **SAC**: amortização constante = PV/n.
- **Amortização extraordinária** (`SimulateExtraordinary`): ReduceTerm (mantém parcela, encurta prazo),
  ReducePayment (mantém prazo, reduz parcela), FullSettlement (quita). Retorna antes/depois + economia.

## Endpoints (todos sob `feature.loans.access`, Intermediate+)

| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/v1/loans` | Lista contratos do usuário |
| GET | `/api/v1/loans/{id}` | Detalhe do contrato |
| GET | `/api/v1/loans/{id}/timeline` | Linha do tempo (eventos agregados) |
| POST | `/api/v1/loans` | Cria contrato |
| PUT | `/api/v1/loans/{id}` | Edita (bloqueado se há parcela paga) |
| DELETE | `/api/v1/loans/{id}` | Exclui (só sem histórico) |
| POST | `/api/v1/loans/{id}/archive` · `/cancel` | Arquiva / cancela (preserva histórico) |
| POST | `/api/v1/loans/simulate` · `/simulations` | Simula |
| POST | `/api/v1/loans/simulations/compare` | Compara PRICE×SAC |
| GET/POST | `/api/v1/loans/{c}/installments/{i}/payments` | Lista / registra pagamento |
| POST | `.../payments/{p}/reverse` · `/receipt` | Estorna / anexa comprovante |
| POST | `/api/v1/loans/{c}/amortizations/simulate` · (base) | Simula / confirma amortização |

Erros seguem **Problem Details** com `code` (ex.: `loan_has_history`, `loan_not_active`,
`installment_already_paid`, `payment_already_reversed`, `invalid_account`, `no_open_installments`) e
`correlationId`.

## Regras de negócio

- **Pagamento** cria `LoanPayment` + movimentação em conta (`AccountTransaction` Debit,
  `source=LoanPayment`), atualiza a parcela e o acompanhamento do contrato — **em uma única transação**.
- **Quitação automática**: pagar a última parcela fecha o contrato (`Closed`, saldo zero).
- **Reversão** de pagamento credita a conta, reabre a parcela (e o contrato, se estava quitado) e
  **preserva** o pagamento (`ReversedAt`).
- **Amortização**: confirma em transação única — grava o registro, movimenta a conta, **regenera as
  parcelas futuras** (pagas preservadas) e atualiza/quita o contrato. Exibe disclaimer de estimativa.
- **Exclusão**: contrato **com** parcelas pagas não pode ser excluído — apenas **arquivado**.

## Idempotência e concorrência

- Pagamentos e amortizações aceitam header **`Idempotency-Key`** (ou chave no corpo). Índice único
  `(UserId, IdempotencyKey)` + checagem prévia → repetir a requisição **não** duplica pagamento/despesa/
  movimentação. Corrida tratada no `DbUpdateException`.
- `LoanContract`/`LoanInstallment` usam token `Version` (concorrência otimista, portável Postgres+SQLite).

## Migrations (EF)

Aditivas, com backfill, **ainda não aplicadas**:
1. `ExpandLoanContractAndInstallment` — SpaceId, campos financeiros, `Version` + backfill (SpaceId =
   espaço default; enums nunca vazios; saldo recomputado). Backfill é **específico do Postgres**
   (guardado por `migrationBuilder.ActiveProvider`).
2. `AddLoanPayments` — tabela `loan_payments` (+ índice único de idempotência).
3. `AddLoanAmortizations` — tabela `loan_amortizations`.

## Integração financeira

Pagamento/amortização geram `AccountTransaction` (Debit) → refletem em **conta, fluxo de caixa e
account-analytics** automaticamente. **Decisão (aprovada):** não se cria uma "Despesa" duplicada;
a inclusão de empréstimos como fonte em **relatórios/orçamento/calendário** está **adiada** (Fase 6
cross-module) para evitar dupla contagem e ser validada com cuidado.

## Autorização / Isolamento

- Backend: policy `feature.loans.access`; toda query filtra por `UserId` (+ `SpaceId` no contrato).
  Conta usada no pagamento é validada por posse (`invalid_account`).
- Frontend: rota `/emprestimos` e `/emprestimos/:id` com `authGuard + roleGuard` (`minRole: Intermediate`).

## UX

- Página **"Empréstimos e financiamentos"**: indicadores, form criar/simular, **Comparar PRICE×SAC**,
  cards com filtro (ativos/quitados/arquivados), **Arquivar** (quando há histórico) e **Ver detalhes**.
- **Sheet de pagamento**: data, conta, multa, desconto, total ao vivo (idempotente).
- **Detalhe `/emprestimos/:id`** com abas: **Resumo**, **Parcelas** (pagar + histórico + estornar),
  **Evolução** (gráfico do saldo devedor), **Histórico** (timeline). **Registrar amortização** com
  preview antes→depois + economia + disclaimer.
- Design system reutilizado (PageHeader, SegmentedSelector, StatusBadge, ConfirmSheet, TransactionSummaryCard,
  UsageBar, UiState/EmptyState); pt-BR/BRL; sem `confirm()/alert()`; `OnPush` + signals + `takeUntilDestroyed`.

## Testes

- Backend: `LoanCalculatorTests` (PRICE/SAC/amortização/conversões), `LoansServiceTests`,
  `LoanPaymentServiceTests`, `LoanAmortizationServiceTests`, `LoanTimelineServiceTests`,
  `LoanDeleteSqliteIntegrationTests`. Total suíte **634**.
- Frontend: `loans.component.spec` + `loan-detail.component.spec` (pagamento, arquivar, comparar,
  reversão, amortização, timeline). Total suíte **530**.

## Como testar no DEV (após deploy)

1. Criar contrato (PRICE e SAC) e comparar.
2. Pagar 1ª parcela **com conta** → validar `AccountTransaction` (Debit) e despesa no fluxo de caixa.
3. Repetir a mesma requisição (Idempotency-Key) → **não** duplica.
4. Estornar o pagamento → conta creditada, parcela reaberta.
5. Registrar amortização (reduzir prazo) → conferir novo cronograma + economia.
6. Pagar todas → contrato **quitado**; tentar excluir com histórico → **arquivar**.
7. Detalhe: abas Evolução (gráfico) e Histórico (timeline).

## Decisões / Limitações / Roadmap

- **Decisões documentadas**: half-up; taxa anual nominal default (efetiva disponível); SpaceId = espaço
  default individual (Família/CNPJ adiados); pagamento→conta é escrituração interna (não gateway).
- **Adiado**: pagamento parcial com split; salvar simulação persistida; wizard de 5 etapas; estorno de
  amortização; estratégias Antecipar-parcelas e Registrar-cálculo-do-banco; quitação antecipada com valor
  negociado; **Documentos** (`LoanDocument`); empréstimos como fonte em **relatórios/orçamento/calendário**.
- **Roadmap**: validar em DEV → E2E (Playwright) + a11y → integração cross-module → documentos.
