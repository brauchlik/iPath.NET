#!/usr/bin/env bash
set -euo pipefail

CONTAINER="ipath2-mysql"
DATABASE="ipath2"
PASSWORD="1cePath"
BACKUP_DIR="$(dirname "$0")/backups"

# Find the newest .sql.gz in backups folder
BACKUP_FILE=$(ls -t "$BACKUP_DIR"/*.sql.gz 2>/dev/null | head -1)

if [ -z "$BACKUP_FILE" ]; then
    echo "ERROR: No .sql.gz backup found in $BACKUP_DIR/"
    exit 1
fi

echo "Restoring backup into MySQL ($DATABASE)..."

podman cp "$BACKUP_FILE" "${CONTAINER}:/tmp/backup.sql.gz"
podman exec "$CONTAINER" sh -c "gunzip -c /tmp/backup.sql.gz | mysql -uroot -prootpw $DATABASE"
podman exec "$CONTAINER" sh -c "rm /tmp/backup.sql.gz"

echo "Backup restored successfully!"

# Configure ipath user
echo "Configuring 'ipath' user for import service access..."
podman exec -i "$CONTAINER" mysql -uroot -prootpw "$DATABASE" <<SQL
CREATE USER IF NOT EXISTS 'ipath'@'%' IDENTIFIED BY '$PASSWORD';
GRANT ALL PRIVILEGES ON $DATABASE.* TO 'ipath'@'%';
FLUSH PRIVILEGES;
SQL

echo "User 'ipath' granted full access to $DATABASE.*"

# Verify connection
echo "Verifying connection..."
RESULT=$(echo "SELECT 'OK';" | podman exec -i "$CONTAINER" mysql -uipath -p"$PASSWORD" "$DATABASE" -N 2>/dev/null || true)

if [ "$RESULT" = "OK" ]; then
    echo "Connection test passed!"
else
    echo "WARNING: Connection test failed. Check user grants."
fi

echo ""
echo "All done. Import service can connect using:"
echo "  Server=127.0.0.1;Database=$DATABASE;Uid=ipath;Pwd=$PASSWORD;Charset=latin1;"
echo ""
echo "Next: run 'dotnet run' from the DataImport directory"
