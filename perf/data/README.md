# Dados de teste do k6

Esta pasta guarda arquivos de entrada usados pelos scripts de carga.

## Arquivo esperado

- `user-snapshot.json`

## Como gerar

Com um token JWT válido:

```bash
curl -sS \
  -H "Authorization: Bearer <TOKEN>" \
  "http://localhost:5059/api/v1/dataportability/export" \
  -o perf/data/user-snapshot.json
```

## Como usar no import load

```bash
k6 run \
  -e PERF_CONFIG=./perf/config/local.json \
  -e IMPORT_FILE=./perf/data/user-snapshot.json \
  perf/scripts/dataportability-import-load.js
```

## Regra de manutenção

- não versionar snapshots sensíveis de usuário real
- usar apenas massa anonimizada ou descartável
