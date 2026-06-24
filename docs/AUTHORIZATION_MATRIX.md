# Matriz de Autorizacao por Endpoint

Fonte de verdade para controle de acesso no backend.

## Objetivo

Este documento existe para:

- registrar a protecao real aplicada pela API
- reduzir divergencia entre codigo, documentacao e UX
- deixar claro qual policy protege cada grupo de endpoint
- sinalizar desalinhamentos conhecidos entre regra atual e direcao de produto

## Como ler este arquivo

- este documento descreve o que o backend protege hoje
- ele nao substitui a direcao de produto em `../../docs/PRODUCT.md`
- quando houver diferenca entre produto desejado e codigo atual, o desalinhamento deve ser marcado explicitamente aqui

Regra pratica:

- frontend pode esconder modulos por UX
- backend continua sendo a fonte final de autorizacao
- mudanca de `[Authorize(Policy = ...)]`, role minima ou feature gate exige atualizacao deste arquivo
- por legibilidade, os exemplos de rota abaixo omitem restricoes de template como `:guid` e `:int`, mas o desenho deve continuar equivalente ao controller real

## Relacao com outros normativos

- este documento deve ser lido junto com `./Agent.md`
- padrao de implementacao e checklist de mudanca backend ficam em `./BACKEND_PADROES_IMPLEMENTACAO.md`
- contrato de erro e status HTTP fica em `./API_CONTRATO_ERROS.md`

## Como interpretar os grupos abaixo

- a `policy real` e sempre a protecao aplicada hoje no controller
- a `faixa funcional` e uma leitura operacional para produto e UX, nao substitui a policy real
- se houver conflito entre faixa funcional e controller, o controller vence e o desalinhamento deve ser registrado aqui

## Hierarquia de perfis

`Basic < Intermediate < Advanced < Admin`

## Policies atuais

### Policies por role minima

- `admin.only`
- `role.atLeast.basic`
- `role.atLeast.intermediate`
- `role.atLeast.advanced`

Observacao:

- nem toda policy existente precisa estar em uso neste momento
- os controllers de aplicacao estao protegidos por feature gate; role minima fica disponivel como primitiva de seguranca

### Policies por feature

- `feature.auth.security.manage`
- `feature.billing.manage`
- `feature.subscriptions.manage`
- `feature.profile.manage`
- `feature.preferences.manage`
- `feature.onboarding.manage`
- `feature.notifications.access`
- `feature.lookups.read`
- `feature.data-portability.export`
- `feature.data-portability.import`
- `feature.plans.manage`
- `feature.incomes.read`
- `feature.incomes.summary.read`
- `feature.budget.access`
- `feature.scenarios.access`
- `feature.reports.access`
- `feature.goals.manage`
- `feature.goal-contributions.manage`
- `feature.installments.read`
- `feature.installments.pay`
- `feature.installments.manage`
- `feature.installments.anticipate`
- `feature.investments.access`
- `feature.accounts.read`
- `feature.accounts.manage`
- `feature.accounts.analytics`
- `feature.accounts.import`
- `feature.cards.read`
- `feature.cards.create-update`
- `feature.cards.delete`
- `feature.cards.statements`
- `feature.categories.read`
- `feature.categories.manage`
- `feature.invoice-import.access`
- `feature.financial-assistant.access`
- `feature.monthly-snapshots.access`
- `feature.loans.access`
- `feature.admin.users.manage`
- `feature.admin.parameters.manage`
- `feature.admin.robots.manage`
- `feature.admin.categories.manage`

## Catalogo de features

- `auth.security.manage`
- `billing.manage`
- `subscriptions.manage`
- `profile.manage`
- `preferences.manage`
- `onboarding.manage`
- `notifications.access`
- `lookups.read`
- `data-portability.export`
- `data-portability.import`
- `plans.manage`
- `incomes.read`
- `incomes.summary.read`
- `budget.access`
- `scenarios.access`
- `reports.access`
- `goals.manage`
- `goal-contributions.manage`
- `installments.read`
- `installments.pay`
- `installments.manage`
- `installments.anticipate`
- `investments.access`
- `accounts.read`
- `accounts.manage`
- `accounts.analytics`
- `accounts.import`
- `cards.read`
- `cards.create-update`
- `cards.delete`
- `cards.statements`
- `categories.read`
- `categories.manage`
- `invoice-import.access`
- `financial-assistant.access`
- `monthly-snapshots.access`
- `loans.access`
- `admin.users.manage`
- `admin.parameters.manage`
- `admin.robots.manage`
- `admin.categories.manage`

## Regra de resolucao de acesso

- `Admin` tem bypass global de feature
- para perfis nao-admin, a decisao usa role efetiva e matriz de features por perfil
- claims explicitas `feature` e `feature_deny` podem complementar ou negar acesso
- overrides por usuario sao aplicados no backend via `IClaimsTransformation`

## Leitura rapida por faixa de acesso

- `Publico`: autenticacao publica, recuperacao de acesso e webhook do provedor
- `Basic via feature`: seguranca da conta, perfil, preferencias, onboarding, notificacoes, consultas gerais, portabilidade (exportacao), billing self-service (incluindo refund, trial e retry-payment), assinaturas, planos, receitas (lista simples), metas, parcelas basicas, leitura de contas, cartoes basicos e leitura de categorias
- `Intermediate+ via feature`: gestao e analytics de contas, faturas de cartao, gestao de categorias, importacoes (incluindo `data-portability.import`), antecipacao de parcelas, assistente financeiro, snapshots mensais, emprestimos (incluindo pagamento de parcela), receitas com analytics, orcamento mensal, simulador de cenarios e relatorios mensais
- `Advanced+ via feature`: investimentos e market data
- `Administrativo via feature`: usuarios, parametros, robos e categorias padrao

## Checklist minimo de validacao

- revisar o controller e confirmar a `policy real` aplicada hoje
- revisar `AppAuthorizationPolicies` e a feature correspondente
- revisar impacto em claims, overrides e bypass de `Admin`
- revisar consumidores impactados no frontend e em fluxos administrativos
- revisar testes de autorizacao e smoke dos endpoints alterados
- revisar `./API_CONTRATO_ERROS.md` quando houver mudanca de status HTTP, erro de acesso ou comportamento de billing/auth

## Mapeamento atual por grupo

### Publico

Sem autenticacao:

Classificacao:

- `policy real`: `AllowAnonymous`
- `faixa funcional`: publico
- `sensivel`: auth, recuperacao de acesso, webhook de billing

- `POST /api/v1/auth/register`
- `POST /api/v1/auth/login`
- `POST /api/v1/auth/refresh`
- `POST /api/v1/auth/forgot-password`
- `POST /api/v1/auth/reset-password`
- `POST /api/v1/auth/logout`
- `POST /api/v1/billing/stripe/webhook`
- `POST /api/v1/billing/mercadopago/webhook`

### Basic via feature gate

Protegidos por feature, nao por role minima direta no controller:

Classificacao:

- `policy real`: feature gate por endpoint
- `faixa funcional`: base comum e self-service
- `sensivel`: auth, billing, subscriptions, exclusao de conta

- `feature.auth.security.manage`
  - `POST /api/v1/auth/change-password`

- `feature.profile.manage`
  - `GET /api/v1/profile`
  - `PUT /api/v1/profile`
  - `POST /api/v1/profile/avatar`

- `feature.preferences.manage`
  - `GET /api/v1/preferences`
  - `PUT /api/v1/preferences`
  - `GET /api/v1/preferences/privacy-summary`
  - `GET /api/v1/preferences/security-summary`
  - `POST /api/v1/preferences/sessions/revoke`
  - `POST /api/v1/preferences/account/delete`

- `feature.onboarding.manage`
  - `GET /api/v1/onboarding`
  - `PUT /api/v1/onboarding`

- `feature.notifications.access`
  - `GET /api/v1/notifications`
  - `POST /api/v1/notifications/generate`
  - `POST /api/v1/notifications/{id}/read`

- `feature.lookups.read`
  - `GET /api/v1/lookups/payment-methods`
  - `GET /api/v1/lookups/card-brands`
  - `GET /api/v1/lookups/institutions`

- `feature.data-portability.export`
  - `GET /api/v1/data-portability/export`

- `feature.data-portability.import`
  - `POST /api/v1/data-portability/import`

- `feature.subscriptions.manage`
  - `GET /api/v1/subscriptions/catalog`
  - `POST /api/v1/subscriptions/change`
  - `POST /api/v1/subscriptions/cancel`
  - `POST /api/v1/subscriptions/refund`
  - `POST /api/v1/subscriptions/request-trial`
  - `POST /api/v1/subscriptions/retry-payment`

- `feature.billing.manage`
  - `POST /api/v1/billing/checkout`
  - `GET /api/v1/billing/checkout-status/{checkoutId}`
  - `GET /api/v1/billing/checkout-status/by-session/{sessionId}`
  - `POST /api/v1/billing/portal`

- `feature.plans.manage`
  - `GET /api/v1/plans`
  - `POST /api/v1/plans`
  - `GET /api/v1/plans/{id}`
  - `PUT /api/v1/plans/{id}`
  - `DELETE /api/v1/plans/{id}`

- `feature.incomes.read`
  - `GET /api/v1/incomes`

- `feature.incomes.summary.read`
  - `GET /api/v1/incomes/summary`

- `feature.goals.manage`
  - `GET /api/v1/goals/income`
  - `PUT /api/v1/goals/income`
  - `GET /api/v1/goals`
  - `POST /api/v1/goals`
  - `GET /api/v1/goals/{id}`
  - `PUT /api/v1/goals/{id}`
  - `DELETE /api/v1/goals/{id}`

- `feature.goal-contributions.manage`
  - `GET /api/v1/goals/{goalId}/contributions`
  - `POST /api/v1/goals/{goalId}/contributions`

- `feature.installments.read`
  - `GET /api/v1/installments`
  - `GET /api/v1/installments/{id}/payments`

- `feature.installments.pay`
  - `POST /api/v1/installments/{id}/payments`
  - `POST /api/v1/installments/{id}/payments/{paymentId}/reversals`

- `feature.installments.manage`
  - `DELETE /api/v1/installments/{id}`

- `feature.accounts.read`
  - `GET /api/v1/accounts`
  - `GET /api/v1/accounts/{id}/balance`
  - `GET /api/v1/accounts/{id}/transactions`

- `feature.categories.read`
  - `GET /api/v1/categories`

Regra atual:

- `Basic` recebe os recursos comuns acima, `accounts.read`, `cards.read`, `cards.create-update`, `cards.delete`, `categories.read` e `incomes.read` (lista simples de receitas, sem analytics)
- `Basic` nao recebe faturas de cartao, gestao de contas, analytics, importacoes, antecipacao, assistente financeiro, snapshots, emprestimos, orcamento, simulador de cenarios, relatorios, `incomes.summary.read` nem investimentos

### Intermediate+ via feature gate

Protegidos por feature, nao por role minima direta no controller:

Classificacao:

- `policy real`: feature gate por endpoint
- `faixa funcional`: operacao financeira intermediaria
- `sensivel`: importacao, analytics, assistente financeiro

- `feature.accounts.manage`
  - `POST /api/v1/accounts`
  - `PUT /api/v1/accounts/{id}`
  - `DELETE /api/v1/accounts/{id}`
  - `POST /api/v1/accounts/transfers`

- `feature.accounts.analytics`
  - `GET /api/v1/accounts/summary/real-balance`
  - `GET /api/v1/accounts/summary/debts`
  - `GET /api/v1/accounts/summary/net-worth`
  - `GET /api/v1/accounts/summary/net-worth/history`
  - `GET /api/v1/accounts/summary/projection`
  - `GET /api/v1/accounts/summary/risk`
  - `GET /api/v1/accounts/summary/insights`
  - `GET /api/v1/accounts/summary/recommendations`

- `feature.accounts.import`
  - `POST /api/v1/accounts/ofx/extract`
  - `POST /api/v1/accounts/ofx/import`
  - `POST /api/v1/accounts/csv/extract`
  - `POST /api/v1/accounts/csv/import`

- `feature.cards.read`
  - `GET /api/v1/cards`
  - `GET /api/v1/cards/debt/total`

- `feature.cards.create-update`
  - `POST /api/v1/cards`
  - `PUT /api/v1/cards/{id}`

- `feature.cards.delete`
  - `DELETE /api/v1/cards/{id}`

- `feature.cards.statements`
  - `GET /api/v1/cards/{id}/statements`

- `feature.categories.manage`
  - `POST /api/v1/categories`
  - `PUT /api/v1/categories/{id}`
  - `DELETE /api/v1/categories/{id}`

- `feature.invoice-import.access`
  - `POST /api/v1/invoice-import/extract`
  - `POST /api/v1/invoice-import/import`
  - `POST /api/v1/invoice-import/reconcile`

- `feature.installments.anticipate`
  - `POST /api/v1/installments/{id}/anticipations`

- `feature.financial-assistant.access`
  - `GET /api/v1/financial-assistant/context`
  - `POST /api/v1/financial-assistant/chat`

- `feature.monthly-snapshots.access`
  - `GET /api/v1/monthly-snapshots`
  - `POST /api/v1/monthly-snapshots/generate`

- `feature.loans.access`
  - `GET /api/v1/loans`
  - `POST /api/v1/loans/simulate`
  - `POST /api/v1/loans`
  - `PUT /api/v1/loans/{id}`
  - `DELETE /api/v1/loans/{id}`
  - `POST /api/v1/loans/{contractId}/installments/{installmentId}/pay`

- `feature.budget.access`
  - `GET /api/v1/budget/{year}/{month}`
  - `PUT /api/v1/budget/{year}/{month}/items`
  - `DELETE /api/v1/budget/items/{itemId}`

- `feature.scenarios.access`
  - `POST /api/v1/scenarios/simulate`

- `feature.reports.access`
  - `GET /api/v1/reports/monthly-summary/{year}/{month}`

Regra pratica:

- na pratica esses modulos representam a fronteira funcional do `Intermediate`
- role sozinha nao basta; a feature efetiva no backend e a protecao real
- quando houver regra adicional dentro do controller ou service, ela tambem deve aparecer documentada aqui

### Advanced+ via feature gate

Protegidos por `feature.investments.access`:

Classificacao:

- `policy real`: `feature.investments.access`
- `faixa funcional`: investimentos e market data
- `sensivel`: investimentos, importacao B3, sincronizacao externa

- `GET /api/v1/investments/goal`
- `PUT /api/v1/investments/goal`
- `GET /api/v1/investments/allocation-target`
- `PUT /api/v1/investments/allocation-target`
- `GET /api/v1/investments/positions`
- `POST /api/v1/investments/positions`
- `PUT /api/v1/investments/positions/{id}`
- `DELETE /api/v1/investments/positions/{id}`
- `GET /api/v1/investments/positions/{id}`
- `POST /api/v1/investments/positions/{id}/movements`
- `GET /api/v1/investments/benchmarks`
- `GET /api/v1/investments/market/quote`
- `GET /api/v1/investments/market/profile`
- `GET /api/v1/investments/market/history`
- `POST /api/v1/investments/import/b3/extract`
- `POST /api/v1/investments/import/b3/confirm`
- `GET /api/v1/investments/b3/consent`
- `POST /api/v1/investments/b3/consent/mock-grant`
- `POST /api/v1/investments/b3/sync`

Regra pratica:

- esse modulo representa hoje a camada mais claramente posicionada no `Advanced`

### Administrativo via feature gate

Protegidos por feature administrativa:

Classificacao:

- `policy real`: feature administrativa por endpoint
- `faixa funcional`: administracao interna
- `sensivel`: usuarios, parametros, robos, categorias padrao

- `feature.admin.users.manage`
  - `GET /api/v1/admin/users`
  - `PUT /api/v1/admin/users/{id}/role`
  - `PUT /api/v1/admin/users/{id}/status`
  - `GET /api/v1/admin/users/{id}/features`
  - `PUT /api/v1/admin/users/{id}/features/{featureKey}`
  - `DELETE /api/v1/admin/users/{id}/features/{featureKey}`
  - `DELETE /api/v1/admin/users/{id}`

- `feature.admin.parameters.manage`
  - `GET /api/v1/admin/parameters/scalability-runtime`
  - `GET /api/v1/admin/parameters/payment-methods`
  - `POST /api/v1/admin/parameters/payment-methods`
  - `PUT /api/v1/admin/parameters/payment-methods/{id}/status`
  - `GET /api/v1/admin/parameters/card-brands`
  - `POST /api/v1/admin/parameters/card-brands`
  - `PUT /api/v1/admin/parameters/card-brands/{id}/status`
  - `GET /api/v1/admin/parameters/institutions`
  - `POST /api/v1/admin/parameters/institutions`
  - `PUT /api/v1/admin/parameters/institutions/{id}/status`
  - `GET /api/v1/admin/parameters/notification-settings`
  - `PUT /api/v1/admin/parameters/notification-settings`
  - `GET /api/v1/admin/parameters/robot-settings`
  - `PUT /api/v1/admin/parameters/robot-settings`
  - `POST /api/v1/admin/parameters/test-email`

- `feature.admin.robots.manage`
  - `GET /api/v1/admin/robots/monitor`
  - `POST /api/v1/admin/robots/run/{robotName}`
  - `POST /api/v1/admin/robots/run-all`

- `feature.admin.categories.manage`
  - `GET /api/v1/admin/categories`
  - `POST /api/v1/admin/categories`
  - `PUT /api/v1/admin/categories/{id}`
  - `PUT /api/v1/admin/categories/{id}/status`

## Desalinhamentos e cuidados atuais

### Documentar feature gate, nao so rotulo de plano

- o documento deve continuar refletindo a feature real aplicada em cada controller
- chamar um modulo apenas de `Basic`, `Intermediate+` ou `Advanced+` sem citar a feature pode esconder a protecao efetiva

### Preferir endpoint explicito a wildcard

- sempre que possivel, liste a rota exata em vez de usar `*`
- wildcard so deve permanecer quando o agrupamento nao esconder diferenca relevante de policy ou comportamento

### Nao confundir feature administrativa com role `Admin`

- os modulos administrativos atuais usam feature gate, nao `admin.only`
- `Admin` possui bypass global de feature, mas o documento deve continuar refletindo a policy realmente aplicada no controller
- se algum endpoint passar a exigir role `Admin` de forma estrita, isso precisa ser registrado explicitamente aqui

## Regra operacional

- frontend pode esconder botoes e menus por UX
- backend sempre aplica policy e e a fonte final de autorizacao
- qualquer alteracao de acesso deve atualizar este arquivo e os atributos `[Authorize(Policy = ...)]`
- quando a direcao de produto mudar antes do codigo, o desalinhamento deve ser registrado aqui ate a protecao real ser ajustada

## Quando atualizar este documento

- mudanca de `[Authorize(Policy = ...)]` em controller
- criacao de nova policy ou nova feature
- alteracao de role minima efetiva
- mudanca de endpoint administrativo
- alinhamento entre produto e backend que altere a protecao real de um modulo
