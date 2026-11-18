# Deploy com GitHub Environments

Este documento define como configurar os environments `development` e `production` usados pelos workflows de deploy da API:

- [../.github/workflows/dotnet-desktop.yml](../.github/workflows/dotnet-desktop.yml): entrega automatica em `development` a partir da `main`.
- [</Users/henriquesantos/Desktop/Codes/InvestindoEmNegocios/InvestindoEmNegociosApi/.github/workflows/deploy-prd.yml>](</Users/henriquesantos/Desktop/Codes/InvestindoEmNegocios/InvestindoEmNegociosApi/.github/workflows/deploy-prd.yml>): promocao manual para `production`.

## Objetivo

O fluxo esperado da API e:

1. gerar uma imagem Docker imutavel no GHCR com tag igual ao `SHA` do commit
2. subir essa imagem em `development`
3. validar o ambiente
4. executar manualmente o workflow `Deploy Production`
5. promover a imagem escolhida para `production`

Segredos nao devem ser commitados nem enviados em arquivo `.env` para o servidor.

## Estado e validação dos ambientes

A topologia de produção versionada está em `docker-compose.prd.yml` e no workflow `deploy-prd.yml`: Caddy como entrada pública, API e PostgreSQL na rede interna. O deploy é manual pelo workflow `Deploy Production`, que recebe `image_tag`; não há campo adicional de confirmação `DEPLOY`.

O registro de pausa de junho de 2026 é histórico e não define sozinho o estado atual da infraestrutura. Este documento descreve a configuração versionada; disponibilidade, imagem publicada e situação do banco devem ser conferidas no ambiente antes de uma operação. A revisão local de 2026-11-15 não realizou deploy nem validou o servidor publicado.

Testes que escrevem dados e validações de cobrança devem usar desenvolvimento ou sandbox. Não usar produção para regressão automatizada.

## Environments obrigatorios

Crie estes GitHub Environments no repositorio:

- `development`
- `production`

Para operador unico, nao use `required reviewers` como bloqueio principal do environment `production`, porque o GitHub pode impedir autoaprovacao. A aprovacao de PRD acontece pelo acionamento manual do workflow `Deploy Production`.

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
8. Atualizar este documento e [roadmap.md](</Users/henriquesantos/Documents/my second brain/Investindo em Negocio/Documentacao/08-backlog/roadmap.md>) removendo a pendencia apos a correcao confirmada.

## Checklist de configuracao

1. Criar `development` e `production` em `Settings > Environments`.
2. Cadastrar os `vars` e `secrets` listados acima em cada environment.
3. Garantir que o environment `production` nao bloqueie autoaprovacao quando houver apenas um operador.
4. Garantir Docker e Docker Compose Plugin instalados no servidor.
5. Garantir que o usuario remoto tenha permissao para rodar Docker.
6. Fazer merge em `main` para validar DEV.
7. Executar manualmente `Deploy Production` para validar PRD.

## Correções de infraestrutura de 2026-11-15

- API, imagem e CI usam .NET 10; executar as ferramentas com SDK 10 instalado.
- `ReverseProxy:KnownNetworks` e `ReverseProxy:KnownProxies` definem somente as origens confiáveis para forwarded headers. Não aceitar cabeçalhos de origem arbitrária. O Compose de produção usa a rede privada explícita em `APP_NETWORK_SUBNET`.
- Comprovantes novos ficam em `/app/storage/receipts`, com volume `private_uploads_prd`. Acesso é autenticado por proprietário e espaço; a rota antiga também passa pela autorização.
- Antes de recriar um container de ambiente existente, preservar os arquivos de `/app/wwwroot/uploads` e disponibilizá-los no novo ambiente para a leitura compatível das URLs anteriores. A revisão não move arquivos no servidor.
- A migration de orçamento e snapshots atribui registros antigos ao espaço padrão e mantém valores e identificadores. Fazer backup do banco antes de promover a migration para um ambiente com dados reais.
- PostgreSQL de regressão é isolado. `IEN_TEST_POSTGRES` habilita os testes de migração e preservação de dados; `IEN_MIGRATIONS_CONNECTION` seleciona explicitamente a conexão das ferramentas de schema, sem carregar segredos da aplicação.

### Identificação pública e bloqueio de publicação

Configurar no GitHub Environment `production` as variables `LEGAL_NAME` (razão social/nome do responsável), `LEGAL_TAX_ID` (CPF/CNPJ válido), `LEGAL_ADDRESS` (endereço completo) e `LEGAL_CONTACT_EMAIL` (contato oficial). Não preencher com fixtures de teste. O workflow repassa esses campos ao Compose e executa a validação da imagem frontend antes do `up`. O SSR também rejeita inicialização de produção sem esses dados.

Esses campos são públicos em termos/privacidade e `/config.js`; não colocar credenciais ou outras informações secretas neles. A validação técnica de formato não comprova titularidade nem substitui revisão jurídica. Dados reais continuam pendentes do responsável.

## Preparação da entrega — 2026-11-18

PR em rascunho: [API #56](https://github.com/Thenrique395/InvestindoEmNegociosApi/pull/56). O workflow `Deliver Backend` usa `docker-compose.deploy.yml` também nas consultas de saúde/logs, igual ao arquivo usado no `up`.

Antes de merge na `main` (que dispara DEV), confirmar banco DEV isolado, preservar comprovantes legados e aguardar CI do SHA final. Validar migrations, readiness, login e regressão financeira em DEV. Produção só depois da validação de API e frontend, com backup/restore verificável, arquivos preservados e imagem imutável por SHA. O frontend relacionado segue bloqueado na auditoria de desenvolvimento; não considerar a entrega conjunta liberada.

### Resultado remoto da preparação de 2026-11-18

[CI Backend 44311077325](https://github.com/Thenrique395/InvestindoEmNegociosApi/actions/runs/44311077325), código `285119d`: 55 smoke e 684 testes aprovados, nenhum ignorado; PostgreSQL incluído e NuGet sem alertas. [API #56](https://github.com/Thenrique395/InvestindoEmNegociosApi/pull/56) e [Web #26](https://github.com/Thenrique395/InvestindoEmNegociosWeb/pull/26) continuam em rascunho. O frontend falhou na auditoria completa no código `c85d963`; nenhum merge/deploy executado. [Estado conjunto da entrega](https://github.com/Thenrique395/InvestindoEmNegociosWeb/blob/chore/consolidacao-validacao-2026-11-18/docs/ai-reports/preparacao-entrega-2026-11-18.md). Estes resultados referem-se aos SHAs citados, anteriores a atualizações exclusivamente documentais.
