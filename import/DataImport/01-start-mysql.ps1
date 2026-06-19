$containerName = "ipath2-mysql"
$password = "1cePath"

# Remove existing container if any
podman rm -f $containerName 2>$null

Write-Host "Starting MySQL 5.7 container ($containerName)..." -ForegroundColor Cyan

podman run -d `
  --name $containerName `
  -e MYSQL_ROOT_PASSWORD=rootpw `
  -e MYSQL_DATABASE=ipath2 `
  -e MYSQL_USER=ipath `
  -e MYSQL_PASSWORD=$password `
  -p 3306:3306 `
  -v ipath2_mysql_data:/var/lib/mysql `
  docker.io/mysql:5.7

Write-Host "Waiting for MySQL to initialize..." -ForegroundColor Yellow
Start-Sleep -Seconds 10

# Verify the container is running
$state = podman inspect $containerName --format "{{.State.Status}}" 2>$null
if ($state -eq "running") {
    Write-Host "Container $containerName is running on port 3306" -ForegroundColor Green
    Write-Host "Connection: Server=127.0.0.1;Database=ipath2;Uid=ipath;Pwd=$password;Charset=latin1;" -ForegroundColor Gray
} else {
    Write-Error "Container failed to start. Run: podman logs $containerName"
}
