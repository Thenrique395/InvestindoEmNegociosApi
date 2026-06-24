# Deploy com GitHub Environments

Este documento define como configurar os environments `development` e `production` usados pelos workflows de deploy da API:

- [../.github/workflows/dotnet-desktop.yml](../.github/workflows/dotnet-desktop.yml): entrega automatica em `development` a partir da `main`.
- [../.github/workflows/deploy-backend-production.yml](../.github/workflows/deploy-backend-production.yml): promocao manual para `production`.

## Objetivo

O fluxo esperado da API e:

1. gerar uma imagem Docker imutavel no GHCR com tag igual ao `SHA` do commit
2. subir essa imagem em `development`
3. validar o ambiente
4. executar manualmente o workflow `Deploy Backend Production`
5. promover a imagem escolhida para `production`

Segredos nao devem ser commitados nem enviados em arquivo `.env` para o servidor.

## Estado atual do ambiente PRD

- o ambiente `production` (`invest-prd`, porta `5060`) esta pausado desde 2026-06-15 — ver [../../docs/DECISIONS/2026-06-15-pausar-ambiente-prd-focar-dev.md](../../docs/DECISIONS/2026-06-15-pausar-ambiente-prd-focar-dev.md)
- testes manuais e automatizados devem usar exclusivamente `development` (`invest-dev`, porta `5059`) até que o ambiente esteja estavel e o PRD seja recriado/validado
- o workflow `Deploy Backend Production` exige digitar `DEPLOY` no input `confirm` para liberar o job, justamente para evitar disparo acidental enquanto o PRD estiver pausado
- a pendencia de inversao dos bancos `meu_mentor_db`/`meu_mentor_prd` (ver "Pendencia conhecida" abaixo) deve ser corrigida antes ou durante a recriacao do PRD

## Environments obrigatorios

Crie estes GitHub Environments no repositorio:

- `development`
- `production`

Para operador unico, nao use `required reviewers` como bloqueio principal do environment `production`, porque o GitHub pode impedir autoaprovacao. A aprovacao de PRD acontece pelo acionamento manual do workflow `Deploy Backend Production`.

Se o time crescer, `required reviewers` pode ser reativado para exigir aprovacao de outra pessoa antes do deploy.

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
- a pipeline envia `docker-compose.deploy.yml` para o servidor
- o schema e aplicado pelo script `scripts/apply-schema-from-db-conn.sh` antes do deploy do container

## Pendencia conhecida

- no servidor atual, os bancos `meu_mentor_db` e `meu_mentor_prd` estao com os papeis de DEV/PRD invertidos em relacao ao nome (`meu_mentor_db` esta servindo PRD e `meu_mentor_prd` esta servindo DEV, ou vice-versa).

### Checklist sugerido para corrigir

1. Confirmar, em cada ambiente (`development` e `production`), qual banco (`meu_mentor_db` ou `meu_mentor_prd`) esta realmente sendo usado hoje (via `DB_CONN` efetivo do container e/ou consulta direta ao Postgres).
2. Registrar o estado atual (qual nome de banco esta em qual ambiente) antes de qualquer mudanca, para permitir rollback.
3. Decidir a estrategia de correcao:
   - renomear os bancos no Postgres para que o nome reflita o ambiente real (`ALTER DATABASE ... RENAME TO ...`), ou
   - ajustar `DB_CONN`/`POSTGRES_DB` em cada `GitHub Environment` para apontar para o banco correto, sem renomear.
4. Fazer backup dos dois bancos antes de qualquer alteracao.
5. Aplicar a mudanca escolhida primeiro em `development`, validar a aplicacao subindo normalmente e respondendo health check.
6. Aplicar a mesma mudanca em `production` em janela de baixo uso, com plano de rollback pronto.
7. Validar `production` com smoke test (login, leitura de dados existentes) antes de considerar concluido.
8. Atualizar este documento e `../../docs/ROADMAP.md` (secao 2.3) removendo a pendencia apos a correcao confirmada.

## Checklist de configuracao

1. Criar `development` e `production` em `Settings > Environments`.
2. Cadastrar os `vars` e `secrets` listados acima em cada environment.
3. Garantir que o environment `production` nao bloqueie autoaprovacao quando houver apenas um operador.
4. Garantir Docker e Docker Compose Plugin instalados no servidor.
5. Garantir que o usuario remoto tenha permissao para rodar Docker.
6. Fazer merge em `main` para validar DEV.
7. Executar manualmente `Deploy Backend Production` para validar PRD.
