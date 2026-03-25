# Matriz de Autorização por Endpoint

Fonte de verdade para controle de acesso no backend.

## Objetivo

Este documento existe para:

- registrar a proteção real aplicada pela API
- reduzir divergência entre código, documentação e UX
- deixar claro qual policy protege cada grupo de endpoint
- sinalizar desalinhamentos conhecidos entre regra atual e direção de produto

## Como ler este arquivo

- este documento descreve o que o backend protege hoje
- ele não substitui a direção de produto em `../../docs/PRODUCT.md`
- quando houver diferença entre produto desejado e código atual, o desalinhamento deve ser marcado explicitamente aqui

Regra prática:

- frontend pode esconder módulos por UX
- backend continua sendo a fonte final de autorização
- mudança de `[Authorize(Policy = ...)]`, role mínima ou feature gate exige atualização deste arquivo

## Hierarquia de perfis

`Basic < Intermediate < Advanced < Admin`

## Policies atuais

### Policies por role mínima

- `admin.only`
- `role.atLeast.basic`
- `role.atLeast.intermediate`
- `role.atLeast.advanced`

Observação:

- nem toda policy existente precisa estar em uso neste momento
- esta seção lista as policies disponíveis no backend, não apenas as já aplicadas em controllers

### Policies por feature

- `feature.investments.access`
- `feature.cards.access`
- `feature.accounts.access`
- `feature.categories.access`
- `feature.invoice-import.access`
- `feature.admin.users.manage`
- `feature.admin.parameters.manage`
- `feature.admin.robots.manage`
- `feature.admin.categories.manage`

## Catálogo de features

- `investments.access`
- `cards.access`
- `accounts.access`
- `categories.access`
- `invoice-import.access`
- `admin.users.manage`
- `admin.parameters.manage`
- `admin.robots.manage`
- `admin.categories.manage`

## Regra de resolução de acesso

- `Admin` tem bypass global de feature
- para perfis não-admin, a decisão usa role efetiva e matriz de features por perfil
- claims explícitas `feature` e `feature_deny` podem complementar ou negar acesso
- overrides por usuário são aplicados no backend via `IClaimsTransformation`

## Leitura rápida por faixa de acesso

- `Público`: autenticação, recuperação de acesso e webhook do provedor
- `Todos os perfis autenticados (role.atLeast.basic)`: perfil, preferências, onboarding, metas, planos, parcelas, notificações, billing self-service e consultas gerais
- `Intermediate+ via feature`: cartões, contas, categorias editáveis, importação e antecipação
- `Advanced+ via feature`: investimentos e market data
- `Administrativo via feature`: usuários, parâmetros, robôs e categorias padrão

## Mapeamento atual por grupo

### Público

Sem autenticação:

- `POST /api/v1/auth/register`
- `POST /api/v1/auth/login`
- `POST /api/v1/auth/refresh`
- `POST /api/v1/auth/forgot-password`
- `POST /api/v1/auth/reset-password`
- `POST /api/v1/auth/logout`
- `POST /api/v1/billing/stripe/webhook`

### Todos os perfis autenticados

Protegidos por `role.atLeast.basic`:

- `POST /api/v1/auth/change-password`
- `GET /api/v1/profile`
- `PUT /api/v1/profile`
- `POST /api/v1/profile/avatar`
- `GET /api/v1/preferences`
- `PUT /api/v1/preferences`
- `GET /api/v1/preferences/privacy-summary`
- `GET /api/v1/preferences/security-summary`
- `POST /api/v1/preferences/sessions/revoke`
- `POST /api/v1/preferences/account/delete`
- `GET /api/v1/onboarding`
- `PUT /api/v1/onboarding`
- `GET /api/v1/receitas/summary`
- `GET /api/v1/plans`
- `POST /api/v1/plans`
- `GET /api/v1/plans/{id}`
- `PUT /api/v1/plans/{id}`
- `DELETE /api/v1/plans/{id}`
- `GET /api/v1/goals/income`
- `PUT /api/v1/goals/income`
- `GET /api/v1/goals`
- `POST /api/v1/goals`
- `GET /api/v1/goals/{id}`
- `PUT /api/v1/goals/{id}`
- `DELETE /api/v1/goals/{id}`
- `GET /api/v1/goals/{goalId}/contributions`
- `POST /api/v1/goals/{goalId}/contributions`
- `GET /api/v1/installments`
- `GET /api/v1/installments/{id}/payments`
- `POST /api/v1/installments/{id}/payments`
- `POST /api/v1/installments/{id}/payments/{paymentId}/reversals`
- `DELETE /api/v1/installments/{id}`
- `GET /api/v1/notifications`
- `POST /api/v1/notifications/generate`
- `POST /api/v1/notifications/{id}/read`
- `GET /api/v1/lookups/payment-methods`
- `GET /api/v1/lookups/card-brands`
- `GET /api/v1/lookups/institutions`
- `GET /api/v1/data-portability/export`
- `POST /api/v1/data-portability/import`
- `GET /api/v1/subscriptions/catalog`
- `POST /api/v1/subscriptions/change`
- `POST /api/v1/subscriptions/cancel`
- `POST /api/v1/billing/checkout`
- `GET /api/v1/billing/checkout-status/{checkoutId}`
- `GET /api/v1/billing/checkout-status/by-session/{sessionId}`
- `POST /api/v1/billing/portal`

### Intermediate+ via feature gate

Protegidos por feature, não só por role nominal:

- `feature.cards.access`
  - `GET /api/v1/cards`
  - `POST /api/v1/cards`
  - `PUT /api/v1/cards/{id}`
  - `DELETE /api/v1/cards/{id}`
  - `GET /api/v1/cards/debt/total`
  - `GET /api/v1/cards/{id}/statements`

- `feature.accounts.access`
  - `GET /api/v1/accounts`
  - `POST /api/v1/accounts`
  - `PUT /api/v1/accounts/{id}`
  - `DELETE /api/v1/accounts/{id}`
  - `GET /api/v1/accounts/{id}/balance`
  - `GET /api/v1/accounts/{id}/transactions`
  - `POST /api/v1/accounts/transfers`
  - `GET /api/v1/accounts/summary/real-balance`
  - `GET /api/v1/accounts/summary/debts`
  - `GET /api/v1/accounts/summary/net-worth`
  - `GET /api/v1/accounts/summary/net-worth/history`
  - `GET /api/v1/accounts/summary/projection`
  - `GET /api/v1/accounts/summary/risk`
  - `GET /api/v1/accounts/summary/insights`
  - `GET /api/v1/accounts/summary/recommendations`
  - `POST /api/v1/accounts/ofx/extract`
  - `POST /api/v1/accounts/ofx/import`
  - `POST /api/v1/accounts/csv/extract`
  - `POST /api/v1/accounts/csv/import`

  Observação importante:

  - além da feature `feature.accounts.access`, o controller aplica bloqueio adicional para `Basic` em mutações de conta
  - hoje `POST|PUT|DELETE /api/v1/accounts` e `POST /api/v1/accounts/transfers` rejeitam `Basic` em runtime, mesmo com feature habilitada
  - endpoints de leitura e sumário permanecem protegidos só pela feature

- `feature.categories.access`
  - `GET /api/v1/categories`
  - `POST /api/v1/categories`
  - `PUT /api/v1/categories/{id}`
  - `DELETE /api/v1/categories/{id}`

- `feature.invoice-import.access`
  - `POST /api/v1/invoice-import/extract`
  - `POST /api/v1/invoice-import/import`
  - `POST /api/v1/invoice-import/reconcile`

- `role.atLeast.intermediate`
  - `POST /api/v1/installments/{id}/anticipations`
  - `GET /api/v1/financialassistant/context`
  - `POST /api/v1/financialassistant/chat`
  - `GET /api/v1/monthlysnapshots`
  - `POST /api/v1/monthlysnapshots/generate`
  - `GET /api/v1/loans`
  - `POST /api/v1/loans/simulate`
  - `POST /api/v1/loans`

Regra prática:

- na prática esses módulos representam a fronteira funcional do `intermediate`
- role sozinha não basta; a feature efetiva no backend é a proteção real
- quando houver regra adicional dentro do controller ou service, ela também deve aparecer documentada aqui

### Advanced+ via feature gate

Protegidos por `feature.investments.access`:

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

Regra prática:

- esse módulo representa hoje a camada mais claramente posicionada no `advanced`

### Administrativo via feature gate

Protegidos por feature administrativa:

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

### Documentar feature gate, não só rótulo de plano

- para `cards`, `accounts`, `categories`, `invoice-import` e `investments`, o documento deve continuar refletindo a feature real
- chamar tudo apenas de `Intermediate+` ou `Advanced+` sem citar a feature pode esconder a proteção efetiva

### Preferir endpoint explícito a wildcard

- sempre que possível, liste a rota exata em vez de usar `*`
- wildcard só deve permanecer quando o agrupamento não esconder diferença relevante de policy ou comportamento

### Não confundir feature administrativa com role `Admin`

- os módulos administrativos atuais usam feature gate, não `admin.only`
- `Admin` possui bypass global de feature, mas o documento deve continuar refletindo a policy realmente aplicada no controller
- se algum endpoint passar a exigir role `Admin` de forma estrita, isso precisa ser registrado explicitamente aqui

## Regra operacional

- frontend pode esconder botões e menus por UX
- backend sempre aplica policy e é a fonte final de autorização
- qualquer alteração de acesso deve atualizar este arquivo e os atributos `[Authorize(Policy = ...)]`
- quando a direção de produto mudar antes do código, o desalinhamento deve ser registrado aqui até a proteção real ser ajustada

## Quando atualizar este documento

- mudança de `[Authorize(Policy = ...)]` em controller
- criação de nova policy ou nova feature
- alteração de role mínima efetiva
- mudança de endpoint administrativo
- alinhamento entre produto e backend que altere a proteção real de um módulo
