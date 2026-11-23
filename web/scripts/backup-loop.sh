#!/bin/sh
set -u

interval="${BACKUP_INTERVAL_SECONDS:-86400}"
retry_interval="${BACKUP_RETRY_SECONDS:-300}"

until pg_isready -h "${PGHOST:-sora-postgres}" -p "${PGPORT:-5432}" -U "${PGUSER:-sora}" -d "${PGDATABASE:-sora}" >/dev/null 2>&1; do
    echo "Waiting for PostgreSQL before the first backup."
    sleep 5
done

while true; do
    if /bin/sh /scripts/backup-once.sh; then
        echo "Next database backup in ${interval} seconds."
        sleep "$interval"
    else
        echo "Backup attempt failed; it will be retried after ${retry_interval} seconds." >&2
        sleep "$retry_interval"
    fi
done
