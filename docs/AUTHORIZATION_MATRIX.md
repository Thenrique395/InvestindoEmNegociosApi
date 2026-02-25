# Matriz de Autorização por Endpoint

Fonte de verdade para controle de acesso no backend.

## Hierarquia de perfis

`Basic < Intermediate < Advanced < Admin`

## Policies

- `admin.only`
- `role.atLeast.basic`
- `role.atLeast.intermediate`
- `role.atLeast.advanced`
- `feature.investments.access`

## Catálogo de features (fase 2)

- `investments.access`
- `cards.access`
- `accounts.access`
- `categories.access`
- `invoice-import.access`
- `admin.users.manage`
- `admin.parameters.manage`
- `admin.robots.manage`
- `admin.categories.manage`

Regra atual:

- `Admin` tem bypass global de feature.
- Para perfis não-admin, a decisão usa matriz de features por perfil.
- Claims explícitas `feature`/`features` no JWT, quando presentes, também são aceitas.

## Mapeamento atual

### Público (sem login)

- `POST /api/v1/auth/register`
- `POST /api/v1/auth/login`
- `POST /api/v1/auth/refresh`
- `POST /api/v1/auth/forgot-password`
- `POST /api/v1/auth/reset-password`
- `POST /api/v1/auth/logout`

### Basic+

- `POST /api/v1/auth/change-password`
- `GET|PUT /api/v1/profile`
- `POST /api/v1/profile/avatar`
- `GET|PUT /api/v1/preferences`
- `GET|PUT /api/v1/onboarding`
- `GET /api/v1/receitas/summary`
- `GET|POST|PUT|DELETE /api/v1/plans`
- `GET|POST /api/v1/goals/{goalId}/contributions`
- `GET|PUT /api/v1/goals/income`
- `GET|POST|PUT|DELETE /api/v1/goals`
- `GET|POST /api/v1/installments`
- `GET|POST /api/v1/installments/{id}/payments`
- `POST /api/v1/installments/{id}/payments/{paymentId}/reversals`
- `POST /api/v1/installments/{id}/anticipations`
- `DELETE /api/v1/installments/{id}`
- `GET|POST /api/v1/notifications`
- `POST /api/v1/notifications/{id}/read`
- `GET /api/v1/lookups/payment-methods`
- `GET /api/v1/lookups/card-brands`
- `GET /api/v1/lookups/institutions`
- `GET /api/v1/data-portability/export`
- `POST /api/v1/data-portability/import`

### Intermediate+

- `GET|POST|PUT|DELETE /api/v1/cards`
- `GET /api/v1/cards/debt/total`
- `GET|POST|PUT|DELETE /api/v1/accounts`
- `GET /api/v1/accounts/{id}/balance`
- `GET /api/v1/accounts/{id}/transactions`
- `GET|POST|PUT|DELETE /api/v1/categories`
- `POST /api/v1/invoice-import/extract`

Obs.: esses módulos já usam policies de feature:

- `feature.accounts.access`
- `feature.cards.access`
- `feature.categories.access`
- `feature.invoice-import.access`

### Advanced+

- `GET|PUT /api/v1/investments/goal`
- `GET|PUT /api/v1/investments/allocation-target`
- `GET|POST|PUT|DELETE /api/v1/investments/positions`
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

Obs.: esse módulo já está protegido por policy de feature (`feature.investments.access`).

### Admin only

- `GET|PUT|DELETE /api/v1/admin/users`
- `GET /api/v1/admin/users/{id}/features`
- `PUT /api/v1/admin/users/{id}/features/{featureKey}`
- `DELETE /api/v1/admin/users/{id}/features/{featureKey}`
- `GET|POST|PUT /api/v1/admin/parameters/*`
- `GET|POST /api/v1/admin/robots/*`
- `GET|POST|PUT /api/v1/admin/categories/*`

Obs.: módulos admin usam policies de feature:

- `feature.admin.users.manage`
- `feature.admin.parameters.manage`
- `feature.admin.robots.manage`
- `feature.admin.categories.manage`

## Regra operacional

- Frontend pode esconder botões/menus por perfil para UX.
- Backend sempre aplica policy e é a fonte final de autorização.
- Overwrite por usuário é aplicado no backend via `IClaimsTransformation` com claims `feature` (allow) e `feature_deny` (deny).
- Qualquer alteração de acesso deve atualizar este arquivo e os atributos `[Authorize(Policy=...)]`.
