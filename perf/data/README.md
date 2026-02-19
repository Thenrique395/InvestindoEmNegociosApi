# Test data for k6

Place snapshot files used by `dataportability-import-load.js` here.

Recommended filename:

- `user-snapshot.json`

## Generate snapshot from API export

Use a valid JWT token:

```bash
curl -sS \
  -H "Authorization: Bearer <TOKEN>" \
  "http://localhost:5059/api/v1/dataportability/export" \
  -o perf/data/user-snapshot.json
```

Then run import load test:

```bash
k6 run \
  -e PERF_CONFIG=./perf/config/local.json \
  -e BASE_URL=http://localhost:5059 \
  -e EMAIL=seu-email@dominio.com \
  -e PASSWORD=sua-senha \
  -e IMPORT_FILE=./perf/data/user-snapshot.json \
  perf/scripts/dataportability-import-load.js
```
