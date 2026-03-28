# syntax=docker/dockerfile:1.9-labs
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:aec87aa74ddf129da573fa69f42f229a23c953a1c6fdecedea1aa6b1fe147d76 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:e362a8dbcd691522456da26a5198b8f3ca1d7641c95624fadc5e3e82678bd08a AS build
ARG BUILD_CONFIGURATION=Release
ARG BUILD_PROJECT=src/Wallow.Api/Wallow.Api.csproj
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

# Now copy everything
COPY . .
# Restore creates obj/ metadata referencing bin/Debug output paths, but .dockerignore
# excludes bin/. MSBuild glob expansion fails traversing the missing directories.
# Create them so globs can resolve cleanly.
RUN find /src/src -name '*.csproj' -execdir mkdir -p bin/Debug/net10.0 bin/Release/net10.0 \;

RUN dotnet build "Wallow.slnx" -c $BUILD_CONFIGURATION --no-restore

FROM build AS migrations
RUN dotnet tool restore
ARG BUILD_CONFIGURATION=Release
RUN dotnet ef migrations bundle \
    --project src/Modules/Identity/Wallow.Identity.Infrastructure/Wallow.Identity.Infrastructure.csproj \
    --startup-project src/Wallow.Api/Wallow.Api.csproj \
    --context IdentityDbContext --output /bundles/efbundle-identity \
    --configuration $BUILD_CONFIGURATION --force --no-build \
    && dotnet ef migrations bundle \
    --project src/Modules/Billing/Wallow.Billing.Infrastructure/Wallow.Billing.Infrastructure.csproj \
    --startup-project src/Wallow.Api/Wallow.Api.csproj \
    --context BillingDbContext --output /bundles/efbundle-billing \
    --configuration $BUILD_CONFIGURATION --force --no-build \
    && dotnet ef migrations bundle \
    --project src/Modules/Storage/Wallow.Storage.Infrastructure/Wallow.Storage.Infrastructure.csproj \
    --startup-project src/Wallow.Api/Wallow.Api.csproj \
    --context StorageDbContext --output /bundles/efbundle-storage \
    --configuration $BUILD_CONFIGURATION --force --no-build \
    && dotnet ef migrations bundle \
    --project src/Modules/Notifications/Wallow.Notifications.Infrastructure/Wallow.Notifications.Infrastructure.csproj \
    --startup-project src/Wallow.Api/Wallow.Api.csproj \
    --context NotificationsDbContext --output /bundles/efbundle-notifications \
    --configuration $BUILD_CONFIGURATION --force --no-build \
    && dotnet ef migrations bundle \
    --project src/Modules/Messaging/Wallow.Messaging.Infrastructure/Wallow.Messaging.Infrastructure.csproj \
    --startup-project src/Wallow.Api/Wallow.Api.csproj \
    --context MessagingDbContext --output /bundles/efbundle-messaging \
    --configuration $BUILD_CONFIGURATION --force --no-build \
    && dotnet ef migrations bundle \
    --project src/Modules/Announcements/Wallow.Announcements.Infrastructure/Wallow.Announcements.Infrastructure.csproj \
    --startup-project src/Wallow.Api/Wallow.Api.csproj \
    --context AnnouncementsDbContext --output /bundles/efbundle-announcements \
    --configuration $BUILD_CONFIGURATION --force --no-build \
    && dotnet ef migrations bundle \
    --project src/Modules/ApiKeys/Wallow.ApiKeys.Infrastructure/Wallow.ApiKeys.Infrastructure.csproj \
    --startup-project src/Wallow.Api/Wallow.Api.csproj \
    --context ApiKeysDbContext --output /bundles/efbundle-apikeys \
    --configuration $BUILD_CONFIGURATION --force --no-build \
    && dotnet ef migrations bundle \
    --project src/Modules/Branding/Wallow.Branding.Infrastructure/Wallow.Branding.Infrastructure.csproj \
    --startup-project src/Wallow.Api/Wallow.Api.csproj \
    --context BrandingDbContext --output /bundles/efbundle-branding \
    --configuration $BUILD_CONFIGURATION --force --no-build \
    && dotnet ef migrations bundle \
    --project src/Modules/Inquiries/Wallow.Inquiries.Infrastructure/Wallow.Inquiries.Infrastructure.csproj \
    --startup-project src/Wallow.Api/Wallow.Api.csproj \
    --context InquiriesDbContext --output /bundles/efbundle-inquiries \
    --configuration $BUILD_CONFIGURATION --force --no-build \
    && dotnet ef migrations bundle \
    --project src/Shared/Wallow.Shared.Infrastructure.Core/Wallow.Shared.Infrastructure.Core.csproj \
    --startup-project src/Wallow.Api/Wallow.Api.csproj \
    --context AuditDbContext --output /bundles/efbundle-audit \
    --configuration $BUILD_CONFIGURATION --force --no-build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG BUILD_PROJECT=src/Wallow.Api/Wallow.Api.csproj
ARG VERSION=0.0.0-local
RUN dotnet publish "$BUILD_PROJECT" -c $BUILD_CONFIGURATION -o /app/publish \
    /p:UseAppHost=false /p:Version=${VERSION} --no-build

FROM base AS final
ARG ENTRYPOINT_DLL=Wallow.Api.dll
ENV ENTRYPOINT_DLL=${ENTRYPOINT_DLL}
WORKDIR /app
COPY --from=publish /app/publish .
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT dotnet ${ENTRYPOINT_DLL}

FROM base AS migrations-runner
WORKDIR /app
COPY --from=migrations /bundles/ bundles/
COPY scripts/apply-migrations.sh .
USER root
RUN chmod +x apply-migrations.sh
USER $APP_UID
ENTRYPOINT ["./apply-migrations.sh"]
