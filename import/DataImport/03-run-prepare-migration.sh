#!/usr/bin/env bash
set -euo pipefail

CONTAINER="ipath2-mysql"
DATABASE="ipath2_prepared"
PASSWORD="1cePath"
SQL_FILE="$(dirname "$0")/doc/prepare-migration.sql"
BACKUP_DIR="$(dirname "$0")/backups"

# Find the newest .sql.gz in backups folder
BACKUP_FILE=$(ls -t "$BACKUP_DIR"/*.sql.gz 2>/dev/null | head -1)

if [ -z "$BACKUP_FILE" ]; then
    echo "ERROR: No .sql.gz backup found in $BACKUP_DIR/"
    exit 1
fi

# --- Phase 0: Drop & recreate database ---
echo "=== Phase 0: Reset database '$DATABASE' ==="
echo "DROP DATABASE IF EXISTS $DATABASE;" | podman exec -i "$CONTAINER" mysql -uroot -prootpw
echo "CREATE DATABASE $DATABASE CHARACTER SET utf8 COLLATE utf8_general_ci;" | podman exec -i "$CONTAINER" mysql -uroot -prootpw

# --- Phase 1: Restore backup ---
echo "=== Phase 1: Restoring backup into '$DATABASE' ==="
podman cp "$BACKUP_FILE" "${CONTAINER}:/tmp/backup.sql.gz"
podman exec "$CONTAINER" sh -c "gunzip -c /tmp/backup.sql.gz | mysql -uroot -prootpw $DATABASE"
podman exec "$CONTAINER" sh -c "rm /tmp/backup.sql.gz"
echo "Restore complete."

# --- Phase 2: Run prepare-migration.sql ---
echo "=== Phase 2: Running prepare-migration.sql ==="
podman exec -i "$CONTAINER" mysql -uroot -prootpw "$DATABASE" -t < "$SQL_FILE"
echo "prepare-migration.sql completed!"

# --- Phase 3: Repeat Level N hierarchy update until 0 rows affected ---
echo "=== Phase 3: Building object hierarchy (_top_id) ==="
COUNT=1
while true; do
    AFFECTED=$(podman exec -i "$CONTAINER" mysql -uroot -prootpw "$DATABASE" -N <<SQL
UPDATE objects child
  JOIN objects parent ON parent.id = child.parent_id
   SET child._top_id = parent._top_id
 WHERE NOT parent._top_id IS NULL
   AND child._top_id IS NULL;
SELECT ROW_COUNT();
SQL
    )
    echo "  Level N pass $COUNT : $AFFECTED rows updated"
    [ "$AFFECTED" = "0" ] && break
    COUNT=$((COUNT + 1))
    [ "$COUNT" -gt 20 ] && { echo "  WARNING: max iterations reached"; break; }
done

# --- Grant ipath user access ---
echo "=== Granting 'ipath' user access to '$DATABASE' ==="
echo "GRANT ALL PRIVILEGES ON $DATABASE.* TO 'ipath'@'%'; FLUSH PRIVILEGES;" | \
    podman exec -i "$CONTAINER" mysql -uroot -prootpw

# --- Sanity: row counts ---
echo "=== Sanity: row counts ==="
podman exec -i "$CONTAINER" mysql -uroot -prootpw "$DATABASE" -t <<SQL
SELECT 'person' AS tbl, COUNT(*) FROM person
UNION ALL SELECT 'community', COUNT(*) FROM community
UNION ALL SELECT 'groups', COUNT(*) FROM groups
UNION ALL SELECT 'objects', COUNT(*) FROM objects
UNION ALL SELECT 'annotation', COUNT(*) FROM annotation;
SQL

echo "=== Done ==="
