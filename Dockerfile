# syntax=docker/dockerfile:1.9-labs
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:aec87aa74ddf129da573fa69f42f229a23c953a1c6fdecedea1aa6b1fe147d76 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:e362a8dbcd691522456da26a5198b8f3ca1d7641c95624fadc5e3e82678bd08a AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy only files needed for restore (better layer caching)
# When only source code changes, the restore layer stays cached
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["global.json", "./"]
COPY ["Wallow.slnx", "./"]

# Copy all .csproj files preserving directory structure (requires BuildKit)
COPY --parents src/**/*.csproj ./
COPY --parents tests/**/*.csproj ./

RUN dotnet restore "Wallow.slnx"

# Now copy everything and build
COPY . .
RUN dotnet build "src/Wallow.Api/Wallow.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG VERSION=0.0.0-local
RUN dotnet publish "src/Wallow.Api/Wallow.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:Version=${VERSION}

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/healthz || exit 1
ENTRYPOINT ["dotnet", "Wallow.Api.dll"]
