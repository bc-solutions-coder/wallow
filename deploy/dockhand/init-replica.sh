#!/bin/sh
# init-replica.sh
# Initializes the PostgreSQL read replica by streaming a base backup from the primary.
# Used as the entrypoint for the postgres-replica container.

set -e

PGDATA="/var/lib/postgresql/data"

# If data directory is empty, perform initial base backup
if [ -z "$(ls -A "$PGDATA" 2>/dev/null)" ]; then
    echo "Replica: No data found. Running pg_basebackup from primary..."

    until pg_isready -h postgres -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -q; do
        echo "Replica: Waiting for primary to be ready..."
        sleep 2
    done

    pg_basebackup \
        -h postgres \
        -U "${POSTGRES_USER}" \
        -D "$PGDATA" \
        -Fp -Xs -R -v

    # Ensure correct permissions
    chmod 0700 "$PGDATA"

    echo "Replica: Base backup complete. Starting in standby mode."
fi

# Start PostgreSQL in replica mode
exec postgres
