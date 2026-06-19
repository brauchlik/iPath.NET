#!/usr/bin/env bash
set -euo pipefail

CONTAINER="ipath2-mysql"
PASSWORD="1cePath"

# Remove existing container if any
podman rm -f "$CONTAINER" 2>/dev/null || true

echo "Starting MySQL 5.7 container ($CONTAINER)..."

podman run -d \
  --name "$CONTAINER" \
  -e MYSQL_ROOT_PASSWORD=rootpw \
  -e MYSQL_DATABASE=ipath2 \
  -e MYSQL_USER=ipath \
  -e MYSQL_PASSWORD="$PASSWORD" \
  -p 3306:3306 \
  -v ipath2_mysql_data:/var/lib/mysql \
  docker.io/mysql:5.7

echo "Waiting for MySQL to initialize..."
sleep 10

# Verify it's running
STATE=$(podman inspect "$CONTAINER" --format '{{.State.Status}}' 2>/dev/null || true)
if [ "$STATE" = "running" ]; then
    echo "Container $CONTAINER is running on port 3306"
    echo "Connection: Server=127.0.0.1;Database=ipath2;Uid=ipath;Pwd=$PASSWORD;Charset=latin1;"
else
    echo "ERROR: Container failed to start. Run: podman logs $CONTAINER"
    exit 1
fi
