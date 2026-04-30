# Deploy com GitHub Environments

Este documento define como configurar os environments `development` e `production` usados pela pipeline em [../.github/workflows/dotnet-desktop.yml](../.github/workflows/dotnet-desktop.yml).

## Objetivo

O fluxo esperado da API e:

1. gerar uma imagem Docker imutavel no GHCR com tag igual ao `SHA` do commit
2. subir essa imagem em `development`
3. validar o ambiente
4. aprovar o environment `production`
5. promover a mesma imagem para `production`

Segredos nao devem ser commitados nem enviados em arquivo `.env` para o servidor.

## Environments obrigatorios

Crie estes GitHub Environments no repositorio:

- `development`
- `production`

No environment `production`, configure `required reviewers` para forcar aprovacao manual antes do deploy.

## Variaveis e segredos usados pela pipeline

### `vars`

Cadastre estes nomes em `development` e `production`:

- `POSTGRES_DB`
- `POSTGRES_USER`
- `POSTGRES_PORT`
- `API_PORT`
- `ASPNETCORE_ENVIRONMENT`
- `PASSWORD_RESET_FRONTEND_URL`
- `PASSWORD_RESET_TOKEN_EXPIRY_MINUTES`
- `SMTP_HOST`
- `SMTP_PORT`
- `SMTP_ENABLE_SSL`
- `SMTP_USER`
- `SMTP_FROM_EMAIL`
- `SMTP_FROM_NAME`
- `STRIPE_PUBLISHABLE_KEY`
- `STRIPE_FRONTEND_BASE_URL`
- `STRIPE_PAYMENT_METHOD_TYPES`

### `secrets`

Cadastre estes nomes em `development` e `production`:

- `VPS_HOST`
- `VPS_USER`
- `VPS_SSH_KEY`
- `POSTGRES_PASSWORD`
- `DB_CONN`
- `JWT_SECRET_KEY`
- `BRAPI_TOKEN`
- `SMTP_PASS`
- `STRIPE_SECRET_KEY`
- `STRIPE_WEBHOOK_SECRET`
- `GHCR_USER`
- `GHCR_TOKEN`

## Valores recomendados por ambiente

### `development`

`vars` sugeridos:

```dotenv
POSTGRES_DB=investindo_dev
POSTGRES_USER=investindo_dev_user
POSTGRES_PORT=5432
API_PORT=5059
ASPNETCORE_ENVIRONMENT=Development
PASSWORD_RESET_FRONTEND_URL=https://dev.seudominio.com/reset-password
PASSWORD_RESET_TOKEN_EXPIRY_MINUTES=30
SMTP_HOST=smtp-relay.brevo.com
SMTP_PORT=587
SMTP_ENABLE_SSL=true
SMTP_USER=CHANGE_ME_DEV_SMTP_USER
SMTP_FROM_EMAIL=no-reply-dev@seudominio.com
SMTP_FROM_NAME=Investindo em Negocios Dev
STRIPE_PUBLISHABLE_KEY=pk_test_CHANGE_ME
STRIPE_FRONTEND_BASE_URL=https://dev.seudominio.com
STRIPE_PAYMENT_METHOD_TYPES=card
```

`secrets` sugeridos:

```dotenv
VPS_HOST=dev-server.seudominio.com
VPS_USER=ubuntu
VPS_SSH_KEY=-----BEGIN OPENSSH PRIVATE KEY-----...
POSTGRES_PASSWORD=CHANGE_ME_DEV_POSTGRES_PASSWORD
DB_CONN=Host=postgres;Port=5432;Database=investindo_dev;Username=investindo_dev_user;Password=CHANGE_ME_DEV_POSTGRES_PASSWORD
JWT_SECRET_KEY=CHANGE_ME_DEV_JWT_SECRET_KEY_MIN_32_CHARS
BRAPI_TOKEN=CHANGE_ME_DEV_BRAPI_TOKEN
SMTP_PASS=CHANGE_ME_DEV_SMTP_PASS
STRIPE_SECRET_KEY=sk_test_CHANGE_ME
STRIPE_WEBHOOK_SECRET=whsec_CHANGE_ME_DEV
GHCR_USER=seu-usuario-ou-bot-ghcr
GHCR_TOKEN=seu-token-com-read-packages
```

### `production`

`vars` sugeridos:

```dotenv
POSTGRES_DB=investindo_prd
POSTGRES_USER=investindo_prd_user
POSTGRES_PORT=5433
API_PORT=5060
ASPNETCORE_ENVIRONMENT=Production
PASSWORD_RESET_FRONTEND_URL=https://app.seudominio.com/reset-password
PASSWORD_RESET_TOKEN_EXPIRY_MINUTES=30
SMTP_HOST=smtp-relay.brevo.com
SMTP_PORT=587
SMTP_ENABLE_SSL=true
SMTP_USER=CHANGE_ME_PRD_SMTP_USER
SMTP_FROM_EMAIL=no-reply@seudominio.com
SMTP_FROM_NAME=Investindo em Negocios
STRIPE_PUBLISHABLE_KEY=pk_live_CHANGE_ME
STRIPE_FRONTEND_BASE_URL=https://app.seudominio.com
STRIPE_PAYMENT_METHOD_TYPES=card
```

`secrets` sugeridos:

```dotenv
VPS_HOST=prd-server.seudominio.com
VPS_USER=ubuntu
VPS_SSH_KEY=-----BEGIN OPENSSH PRIVATE KEY-----...
POSTGRES_PASSWORD=CHANGE_ME_PRD_POSTGRES_PASSWORD
DB_CONN=Host=postgres;Port=5432;Database=investindo_prd;Username=investindo_prd_user;Password=CHANGE_ME_PRD_POSTGRES_PASSWORD
JWT_SECRET_KEY=CHANGE_ME_PRD_JWT_SECRET_KEY_MIN_32_CHARS
BRAPI_TOKEN=CHANGE_ME_PRD_BRAPI_TOKEN
SMTP_PASS=CHANGE_ME_PRD_SMTP_PASS
STRIPE_SECRET_KEY=sk_live_CHANGE_ME
STRIPE_WEBHOOK_SECRET=whsec_CHANGE_ME_PRD
GHCR_USER=seu-usuario-ou-bot-ghcr
GHCR_TOKEN=seu-token-com-read-packages
```

## O que a pipeline assume hoje

Alguns valores nao sao configurados no GitHub Environment porque ja estao fixos no workflow:

- stack `development`: `invest-dev`
- stack `production`: `invest-prd`
- path remoto: `/home/ubuntu/InvestindoEmNegocio`
- imagem base: `ghcr.io/thenrique395/investindoemnegociosapi`

## Observacoes operacionais

- `DB_CONN` deve usar `Host=postgres`, porque esse e o hostname do service no `docker compose`
- o banco continua ouvindo em `5432` dentro da rede Docker; `POSTGRES_PORT` muda apenas a porta publicada no host
- `API_PORT` muda apenas a porta publicada no host; a API continua ouvindo em `5059` dentro do container
- `GHCR_TOKEN` precisa conseguir fazer pull da imagem no servidor
- a pipeline envia apenas `docker-compose.yml` e `InvestindoEmNegocio/Infrastructure/Data/schema.sql`
- o bootstrap do schema acontece via service `schema-bootstrap` antes da API principal subir

## Checklist de configuracao

1. Criar `development` e `production` em `Settings > Environments`.
2. Cadastrar os `vars` e `secrets` listados acima em cada environment.
3. Configurar `required reviewers` no environment `production`.
4. Garantir Docker e Docker Compose Plugin instalados no servidor.
5. Garantir que o usuario remoto tenha permissao para rodar Docker.
6. Fazer um `workflow_dispatch` ou merge em `main` para validar o fluxo completo.
