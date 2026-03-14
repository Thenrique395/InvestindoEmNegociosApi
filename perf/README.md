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
