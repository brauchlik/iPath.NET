

## install postgres
podman run --name postgres -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=test1234 -p 5433:5432 -v /var/lib/data -d postgres


## EF Core Migrations
Startup Project: UI/iPath.Blazor
Configuration: UI/iPath.Blazor/appsettings.json


### Sqlite
dotnet ef migrations add Initial --project Infrastructure/iPath.EF.Sqlite --startup-project UI/iPath.Blazor

### Postgres
dotnet ef migrations add Initial --project Infrastructure/iPath.EF.Postgres --startup-project UI/iPath.Blazor

### update
dotnet ef database update --project Infrastructure/iPath.Persistance --startup-project UI/iPath.Blazor



## Data Import
### install mysql server
podman run -d --name=mariadb -p 3306:3306 -e MYSQL_ROOT_PASSWORD=raussa99 -e MYSQL_USER=ipath -e MYSQL_PASSWORD=1cePath -e MYSQL_DATABASE=ipath2 -v /var/lib/data  mariadb

### load backup
cd DataImport
gunzip < ipath-20250806.sql.gz | mysql -h 127.0.0.1 -u ipath -p ipath2

### prepare import


### run import
cd DataImport
dotnet run --project iPath.DataImport.csproj

