#!/bin/sh
set -e

PGDATA="/var/lib/postgresql/data"

# If data directory is empty, perform initial base backup
if [ -z "$(ls -A "$PGDATA" 2>/dev/null)" ]; then
    echo "Replica: No data found. Running pg_basebackup from primary..."

    until pg_isready -h "${POSTGRES_PRIMARY_HOST:-postgres}" -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -q; do
        echo "Replica: Waiting for primary to be ready..."
        sleep 2
    done

    pg_basebackup \
        -h "${POSTGRES_PRIMARY_HOST:-postgres}" \
        -U "${POSTGRES_USER}" \
        -D "$PGDATA" \
        -Fp -Xs -R -v

    chown -R postgres:postgres "$PGDATA"
    chmod 0700 "$PGDATA"
    echo "Replica: Base backup complete. Starting in standby mode."
fi

exec gosu postgres postgres
