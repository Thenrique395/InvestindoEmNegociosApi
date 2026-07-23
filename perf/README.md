# Testes de Performance com k6

Este diretório concentra os scripts de carga da API.

## Estrutura

- `scripts/login-stress.js`
- `scripts/api-read-load.js`
- `scripts/api-full-suite.js`
- `scripts/dataportability-export-load.js`
- `scripts/dataportability-import-load.js`
- `config/`
- `data/`
- `results/`

## Pré-requisitos

- `k6` instalado
- API acessível
- credenciais válidas

## Configuração

Arquivo recomendado:

```bash
cp perf/config/local.example.json perf/config/local.json
```

Depois disso:

- preencher `baseUrl`
- preencher usuário e senha
- usar `PERF_CONFIG` na execução

## Comandos principais

### Login

```bash
k6 run -e PERF_CONFIG=./perf/config/local.json perf/scripts/login-stress.js
```

### Leituras autenticadas

```bash
k6 run -e PERF_CONFIG=./perf/config/local.json perf/scripts/api-read-load.js
```

### Suíte principal

```bash
k6 run -e PERF_CONFIG=./perf/config/local.json perf/scripts/api-full-suite.js
```

### Suíte principal com escrita

```bash
k6 run \
  -e PERF_CONFIG=./perf/config/local.json \
  -e WRITE_MODE=true \
  -e VUS=10 \
  -e DURATION=3m \
  perf/scripts/api-full-suite.js
```

### Export de portabilidade

```bash
k6 run -e PERF_CONFIG=./perf/config/local.json perf/scripts/dataportability-export-load.js
```

### Import de portabilidade

```bash
k6 run \
  -e PERF_CONFIG=./perf/config/local.json \
  -e IMPORT_FILE=./perf/data/user-snapshot.json \
  perf/scripts/dataportability-import-load.js
```

### Jornadas de usuário logado (leitura + escrita real) — `user-journeys.js`

Simula usuários autenticados usando o app como gente de verdade: navegam no
dashboard (leituras) **e** executam fluxos completos de escrita (conta +
transferência, categoria, cartão, plano parcelado + pagamento, meta +
contribuição, investimento + movimento). Cada fluxo de escrita **faz limpeza**
(cria e apaga no mesmo ciclo), então não acumula lixo.

Diferente dos demais scripts, a auth aqui é a **real da API atual**: cookie
httpOnly (`access_token`) + antiforgery (`X-XSRF-TOKEN`). O `setup()` loga as 3
contas de teste **uma vez** e injeta os cookies em cada VU (o rate-limit de login
é por IP, então os VUs não podem logar individualmente). Cada VU usa uma conta
"casa" (`VU % 3`) e só grava o que o perfil permite (gating real por role).

```bash
# STAGE = smoke | quick | load | stress   (default: load)
PASSWORD='<senha-das-contas-de-teste>' k6 run \
  -e BASE_URL=http://35.174.50.187:5055 \
  -e STAGE=load \
  perf/scripts/user-journeys.js

# contas: default = contas de auditoria; sobrescreva com
#   -e ADV_EMAIL=... -e INT_EMAIL=... -e BASIC_EMAIL=...
```

Os perfis são curtos de propósito (`load` ~3,5 min, `stress` ~5,5 min): os tokens
capturados no setup vivem ~15 min, então manter cada run bem abaixo disso evita
falso "token expirado". Timeout de request curto (10s) + `gracefulStop` impedem
que um servidor lento faça o teste vazar além do agendado.

Métricas-chave: `server_errors` (qualquer 5xx reprova), `auth_errors` (401/403),
`http_req_duration{kind:read|write}`.

## Relatório

```bash
k6 run \
  -e PERF_CONFIG=./perf/config/local.json \
  perf/scripts/api-full-suite.js \
  --summary-export=perf/results/api-full-baseline.json
```

## Alvos iniciais

- `http_req_failed` < `2%`
- auth `p95` < `900ms`
- leituras principais `p95` < `1200ms`
- import `p95` < `3000ms`

## Regra de manutenção

- manter aqui só instrução operacional
- resultados e números históricos ficam em `results/`, não neste README
