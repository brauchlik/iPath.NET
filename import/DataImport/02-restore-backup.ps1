$containerName = "ipath2-mysql"
$password = "1cePath"

# Find the first .sql.gz in the backups folder
$backupFile = Get-ChildItem -Path "$PSScriptRoot\backups" -Filter "*.sql.gz" | Select-Object -First 1

if (-not $backupFile) {
    Write-Error "No .sql.gz backup found in $PSScriptRoot\backups\"
    Write-Host "Place your backup file (e.g. ipath-prod-2026-06-18.sql.gz) in the backups/ folder and re-run." -ForegroundColor Yellow
    exit 1
}

# --- Step 1: Restore the backup ---
Write-Host "Restoring $($backupFile.Name) into MySQL ($containerName)..." -ForegroundColor Cyan

# Copy into container and decompress inside (avoids needing gunzip on Windows host)
podman cp $backupFile.FullName ${containerName}:/tmp/backup.sql.gz
podman exec $containerName sh -c "gunzip -c /tmp/backup.sql.gz | mysql -uroot -prootpw ipath2"
podman exec $containerName sh -c "rm /tmp/backup.sql.gz"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Restore failed. Check that the container is running: podman ps"
    exit 1
}
Write-Host "Backup restored successfully!" -ForegroundColor Green

# --- Step 2: Ensure the import service user exists with network access ---
Write-Host "Configuring 'ipath' user for import service access..." -ForegroundColor Cyan

$sql = @"
-- Create/recreate the ipath user with access from any host
CREATE USER IF NOT EXISTS 'ipath'@'%' IDENTIFIED BY '$password';
GRANT ALL PRIVILEGES ON ipath2.* TO 'ipath'@'%';
FLUSH PRIVILEGES;
"@

$sql | podman exec -i $containerName mysql -uroot -prootpw ipath2

if ($LASTEXITCODE -eq 0) {
    Write-Host "User 'ipath'@'%' granted full access to ipath2.*" -ForegroundColor Green
}

# --- Step 3: Verify connection ---
Write-Host "Verifying connection..." -ForegroundColor Cyan
$testSql = "SELECT 'OK' AS status;"
$testSql | podman exec -i $containerName mysql -uipath -p$password ipath2 -N 2>$null

if ($LASTEXITCODE -eq 0) {
    Write-Host "Connection test passed!" -ForegroundColor Green
} else {
    Write-Warning "Connection test failed. You may need to check the user grants."
}

Write-Host ""
Write-Host "All done. Import service can now connect using:" -ForegroundColor Green
Write-Host "  Server=127.0.0.1;Database=ipath2;Uid=ipath;Pwd=$password;Charset=latin1;" -ForegroundColor Gray
Write-Host ""
Write-Host "Next step: run 'dotnet run' from the DataImport directory" -ForegroundColor Yellow
