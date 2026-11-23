#!/bin/sh
set -eu
umask 077

backup_dir="${BACKUP_DIRECTORY:-/backups}"
retention_count="${BACKUP_RETENTION_COUNT:-7}"
if [ -z "${PGPASSWORD:-}" ]; then
    PGPASSWORD="${POSTGRES_PASSWORD:-}"
    export PGPASSWORD
fi
if [ -z "${PGPASSWORD:-}" ]; then
    echo "Backup failed: PostgreSQL password is not configured." >&2
    exit 1
fi
mkdir -p "$backup_dir"

timestamp="$(date -u '+%Y%m%dT%H%M%SZ')"
target="$backup_dir/sora_${timestamp}.dump"
temporary="$target.partial"

if ! pg_dump --format=custom --no-owner --no-privileges --file="$temporary"; then
    rm -f "$temporary"
    echo "Backup failed: pg_dump returned an error." >&2
    exit 1
fi

if ! pg_restore --list "$temporary" >/dev/null 2>&1; then
    rm -f "$temporary"
    echo "Backup failed: archive validation did not pass." >&2
    exit 1
fi

chmod 600 "$temporary"
mv "$temporary" "$target"

ls -1t "$backup_dir"/sora_*.dump 2>/dev/null \
    | tail -n "+$((retention_count + 1))" \
    | while IFS= read -r old_backup; do rm -f -- "$old_backup"; done

echo "Backup created and archive-validated: $target"
