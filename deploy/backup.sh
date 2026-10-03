#!/usr/bin/env bash
# Dumps the database to a compressed file in ./backups and deletes dumps older than 7 days.
# Restore a dump:   gunzip -c backups/rhythmflow-2026-10-05.sql.gz | docker compose exec -T db psql -U rfadmin rhythmflow
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p backups
file="backups/rhythmflow-$(date +%F).sql.gz"
docker compose exec -T db pg_dump -U rfadmin --no-owner rhythmflow | gzip > "$file"
find backups -name 'rhythmflow-*.sql.gz' -mtime +7 -delete
echo "$(date -Is) backup written: $file ($(du -h "$file" | cut -f1))"
