# =============================================================================
# Dockerfile.geo — .NET Geo gRPC Service
# Build context: repo root (../)
# Targets: dev (hot reload), prod (optimized runtime)
# =============================================================================

# --- Build stage: restore + publish release binaries ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution-level files needed for restore (central package management)
COPY D2.sln Directory.Build.props stylecop.json ./

# Copy all .NET source and proto contracts (needed for Grpc.Tools code gen)
COPY backends/dotnet/ backends/dotnet/
COPY contracts/ contracts/

# Restore dependencies for the Geo API project (pulls transitive refs from sln)
RUN dotnet restore backends/dotnet/services/Geo/Geo.API/Geo.API.csproj

# Publish self-contained release build
RUN dotnet publish backends/dotnet/services/Geo/Geo.API/Geo.API.csproj \
    -c Release -o /app/publish --no-restore

# --- Dev stage: SDK image with hot reload via dotnet watch ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
WORKDIR /src

ENV ASPNETCORE_ENVIRONMENT=Development
# Polling watcher required inside containers (inotify not available on bind mounts)
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
ARG GEO_PORT=5138
ENV ASPNETCORE_URLS=http://+:${GEO_PORT}

EXPOSE ${GEO_PORT}

# Source tree is volume-mounted at runtime for live editing
ENTRYPOINT ["dotnet", "watch", "run", \
    "--no-launch-profile", \
    "--project", "backends/dotnet/services/Geo/Geo.API/Geo.API.csproj"]

# --- Prod stage: minimal ASP.NET runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS prod
WORKDIR /app

ARG GEO_PORT=5138
ENV ASPNETCORE_URLS=http://+:${GEO_PORT}

EXPOSE ${GEO_PORT}

COPY --from=build /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "Geo.API.dll"]
