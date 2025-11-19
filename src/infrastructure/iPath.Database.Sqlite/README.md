# Migrations

## Creating the Initial Migration
` dotnet ef migrations add Initial --startup-project ..\..\ui\iPath.Blazor.Server`

## Applying Migrations
` dotnet ef database update --startup-project ..\..\ui\iPath.Blazor.Server`

## Drop Database
` dotnet ef database drop --startup-project ..\..\ui\iPath.Blazor.Server`
