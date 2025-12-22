# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 5000


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/ui/iPath.Blazor.Server/iPath.Blazor.Server.csproj", "src/ui/iPath.Blazor.Server/"]
COPY ["aspire/iPath.NET.ServiceDefaults/iPath.NET.ServiceDefaults.csproj", "aspire/iPath.NET.ServiceDefaults/"]
COPY ["src/core/iPath.Application/iPath.Application.csproj", "src/core/iPath.Application/"]
COPY ["src/core/iPath.Domain/iPath.Domain.csproj", "src/core/iPath.Domain/"]
COPY ["src/infrastructure/iPath.API/iPath.API.csproj", "src/infrastructure/iPath.API/"]
COPY ["src/infrastructure/iPath.Database.EFCore/iPath.Database.EFCore.csproj", "src/infrastructure/iPath.Database.EFCore/"]
COPY ["src/infrastructure/iPath.Database.Postgres/iPath.Database.Postgres.csproj", "src/infrastructure/iPath.Database.Postgres/"]
COPY ["src/infrastructure/iPath.Database.Sqlite/iPath.Database.Sqlite.csproj", "src/infrastructure/iPath.Database.Sqlite/"]
COPY ["src/infrastructure/iPath.Google/iPath.Google.csproj", "src/infrastructure/iPath.Google/"]
COPY ["src/ui/iPath.Blazor.Client/iPath.Blazor.Client.csproj", "src/ui/iPath.Blazor.Client/"]
COPY ["src/ui/iPath.RazorLib/iPath.Blazor.Componenents.csproj", "src/ui/iPath.RazorLib/"]
COPY ["src/ui/iPath.Blazor.ServiceLib/iPath.Blazor.ServiceLib.csproj", "src/ui/iPath.Blazor.ServiceLib/"]
COPY ["src/ui/iPath.LHCForms/iPath.LHCForms.csproj", "src/ui/iPath.LHCForms/"]
RUN dotnet restore "./src/ui/iPath.Blazor.Server/iPath.Blazor.Server.csproj"
COPY . .
WORKDIR "/src/src/ui/iPath.Blazor.Server"
RUN dotnet build "./iPath.Blazor.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./iPath.Blazor.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "iPath.Blazor.Server.dll"]