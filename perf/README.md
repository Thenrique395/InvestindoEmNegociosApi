# Performance and Load Tests (k6)

This folder contains k6 scripts for API load/performance tests.

## Prerequisites

- k6 installed: https://k6.io/docs/get-started/installation/
- Backend running and reachable (local, docker, or VPS)
- Valid user credentials

## Folder structure

- `scripts/login-stress.js`: login stress/spike test
- `scripts/api-read-load.js`: authenticated read endpoints load test
- `scripts/api-full-suite.js`: full API suite (all major modules)
- `scripts/dataportability-export-load.js`: export endpoint load test
- `scripts/dataportability-import-load.js`: import endpoint load test
- `data/`: input files used by tests
- `results/`: output summary files

## Configuration file (recommended)

Default config file:

- `perf/config/default.json`

Create your local file:

```bash
cp perf/config/local.example.json perf/config/local.json
```

Then fill credentials and URL in `perf/config/local.json`.

Run any script using this file:

```bash
k6 run -e PERF_CONFIG=./perf/config/local.json perf/scripts/api-full-suite.js
```

## Env vars (optional override)

You can override any value from file with env vars (`BASE_URL`, `EMAIL`, `PASSWORD`, `ADMIN_EMAIL`, `ADMIN_PASSWORD`, `WRITE_MODE`, etc.).

## Run tests

### 1) Login stress

```bash
k6 run -e PERF_CONFIG=./perf/config/local.json perf/scripts/login-stress.js
```

### 2) API reads load

```bash
k6 run -e PERF_CONFIG=./perf/config/local.json perf/scripts/api-read-load.js
```

### 3) Full API suite (major modules)

Default read-only:

```bash
k6 run -e PERF_CONFIG=./perf/config/local.json perf/scripts/api-full-suite.js
```

With admin + writes:

```bash
k6 run \
  -e PERF_CONFIG=./perf/config/local.json \
  -e WRITE_MODE=true \
  -e VUS=10 \
  -e DURATION=3m \
  perf/scripts/api-full-suite.js
```

### 4) Data portability export

```bash
k6 run -e PERF_CONFIG=./perf/config/local.json perf/scripts/dataportability-export-load.js
```

### 5) Data portability import

Requires a JSON snapshot file (see `perf/data/README.md`).

```bash
k6 run \
  -e PERF_CONFIG=./perf/config/local.json \
  -e IMPORT_FILE=./perf/data/user-snapshot.json \
  perf/scripts/dataportability-import-load.js
```

## Save reports

```bash
k6 run -e PERF_CONFIG=./perf/config/local.json perf/scripts/api-full-suite.js --summary-export=perf/results/api-full-baseline.json
```

## Suggested first-pass targets

- Errors (`http_req_failed`) < 1-2%
- Auth p95 < 900ms
- Main reads p95 < 1200ms
- Import p95 < 3000ms

Tune targets with production baseline over time.
