#!/usr/bin/env bash
set -euo pipefail

schema_path="${1:-InvestindoEmNegocio/Infrastructure/Data/schema.sql}"

if [ -z "${DB_CONN:-}" ]; then
  echo "DB_CONN is required to apply the database schema."
  exit 1
fi

if [ ! -f "$schema_path" ]; then
  echo "Schema file not found: $schema_path"
  exit 1
fi

eval "$(python3 <<'PY'
import os
import shlex

connection_string = os.environ["DB_CONN"]
parts = {}

for segment in connection_string.split(";"):
    segment = segment.strip()
    if not segment:
        continue
    if "=" not in segment:
        raise SystemExit(f"Invalid DB_CONN segment: {segment}")
    key, value = segment.split("=", 1)
    parts[key.strip().lower()] = value.strip()

values = {
    "PGHOST": parts.get("host") or parts.get("server"),
    "PGPORT": parts.get("port") or "5432",
    "PGDATABASE": parts.get("database") or parts.get("initial catalog"),
    "PGUSER": parts.get("username") or parts.get("user id") or parts.get("userid") or parts.get("user"),
    "PGPASSWORD": parts.get("password") or parts.get("pwd"),
}

missing = [name for name, value in values.items() if not value]
if missing:
    raise SystemExit("DB_CONN is missing required field(s): " + ", ".join(missing))

for key, value in values.items():
    print(f"export {key}={shlex.quote(value)}")
PY
)"

echo "Checking database schema for '$PGDATABASE' on '$PGHOST:$PGPORT'."

for _ in $(seq 1 30); do
  if pg_isready -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$PGDATABASE" >/dev/null 2>&1; then
    break
  fi
  sleep 2
done

if ! pg_isready -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$PGDATABASE" >/dev/null 2>&1; then
  echo "Database is not ready: $PGHOST:$PGPORT/$PGDATABASE"
  exit 1
fi

if psql -v ON_ERROR_STOP=1 -tAc "SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'users'" | grep -q 1; then
  echo "Schema already bootstrapped for '$PGDATABASE'. Applying incremental compatibility section only."
  incremental_sql="$(awk '/^-- Incremental compatibility and newer objects/{found=1} found' "$schema_path")"
  if [ -z "$incremental_sql" ]; then
    echo "No incremental compatibility section found in $schema_path."
    exit 0
  fi
  printf '%s\n' "$incremental_sql" | psql -v ON_ERROR_STOP=1 -1 -f -
  echo "Incremental schema changes applied to '$PGDATABASE'."
  exit 0
fi

echo "Applying schema to '$PGDATABASE'."
psql -v ON_ERROR_STOP=1 -1 -f "$schema_path"
echo "Schema applied to '$PGDATABASE'."
