# Migrations

preparations: set the dbprovider to "Postgres" in
 - in appsettings (iPath-Blazor.Server)
 - user secrets

## Creating the Initial Migration
` dotnet ef migrations add Initial --startup-project ..\..\ui\iPath.Blazor.Server`

## Applying Migrations
` dotnet ef database update --startup-project ..\..\ui\iPath.Blazor.Server`

## Drop Database
` dotnet ef database drop --startup-project ..\..\ui\iPath.Blazor.Server`
