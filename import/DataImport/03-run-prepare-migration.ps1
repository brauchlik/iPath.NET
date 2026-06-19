$containerName = "ipath2-mysql"
$database = "ipath2_prepared"
$password = "1cePath"
$sqlFile = "$PSScriptRoot\doc\prepare-migration.sql"

# Find the newest .sql.gz in the backups folder
$backupFile = Get-ChildItem -Path "$PSScriptRoot\backups" -Filter "*.sql.gz" |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1

if (-not $backupFile) {
    Write-Error "No .sql.gz backup found in $PSScriptRoot\backups\"
    exit 1
}

# --- Phase 0: Drop & recreate database ---
Write-Host "=== Phase 0: Reset database '$database' ===" -ForegroundColor Cyan
"DROP DATABASE IF EXISTS $database;" | podman exec -i $containerName mysql -uroot -prootpw
"CREATE DATABASE $database CHARACTER SET utf8 COLLATE utf8_general_ci;" | podman exec -i $containerName mysql -uroot -prootpw

# --- Phase 1: Restore backup ---
Write-Host "=== Phase 1: Restoring backup into '$database' ===" -ForegroundColor Cyan
podman cp $backupFile.FullName ${containerName}:/tmp/backup.sql.gz
podman exec $containerName sh -c "gunzip -c /tmp/backup.sql.gz | mysql -uroot -prootpw $database"
podman exec $containerName sh -c "rm /tmp/backup.sql.gz"
Write-Host "Restore complete." -ForegroundColor Green

# --- Phase 2: Run prepare-migration.sql ---
Write-Host "=== Phase 2: Running prepare-migration.sql ===" -ForegroundColor Cyan
Get-Content $sqlFile -Raw | podman exec -i $containerName mysql -uroot -prootpw $database -t

if ($LASTEXITCODE -ne 0) {
    Write-Error "prepare-migration.sql failed (exit code: $LASTEXITCODE)"
    exit 1
}
Write-Host "prepare-migration.sql completed!" -ForegroundColor Green

# --- Phase 3: Repeat Level N hierarchy update until 0 rows affected ---
Write-Host "=== Phase 3: Building object hierarchy (_top_id) ===" -ForegroundColor Cyan
$count = 1
do {
    $rows = @"
UPDATE objects child
  JOIN objects parent ON parent.id = child.parent_id
   SET child._top_id = parent._top_id
 WHERE NOT parent._top_id IS NULL
   AND child._top_id IS NULL;
SELECT ROW_COUNT() AS affected;
"@ | podman exec -i $containerName mysql -uroot -prootpw $database -N 2>&1

    $affected = ($rows | Select-String -Pattern '^\d+$' | ForEach-Object { $_.Line } | Select-Object -Last 1)
    Write-Host "  Level N pass $count : $affected rows updated" -ForegroundColor DarkYellow
    $count++
} while ($affected -ne '0' -and $count -le 20)

# --- Grant ipath user access to this database ---
Write-Host "=== Granting 'ipath' user access to '$database' ===" -ForegroundColor Cyan
"GRANT ALL PRIVILEGES ON $database.* TO 'ipath'@'%'; FLUSH PRIVILEGES;" |
    podman exec -i $containerName mysql -uroot -prootpw

# --- Sanity: row counts ---
Write-Host "=== Sanity: row counts ===" -ForegroundColor Cyan
@"
SELECT 'person' AS tbl, COUNT(*) FROM person
UNION ALL SELECT 'community', COUNT(*) FROM community
UNION ALL SELECT 'groups', COUNT(*) FROM groups
UNION ALL SELECT 'objects', COUNT(*) FROM objects
UNION ALL SELECT 'annotation', COUNT(*) FROM annotation;
"@ | podman exec -i $containerName mysql -uroot -prootpw $database -t

Write-Host "=== Done ===" -ForegroundColor Green
