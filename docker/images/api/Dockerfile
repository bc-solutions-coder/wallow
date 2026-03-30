# ============================================
# Stage 1: Restore + Build
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy build config files first for layer caching
COPY Directory.Packages.props Directory.Build.props .editorconfig branding.json ./
COPY src/ src/

# Restore and build
RUN dotnet restore src/Wallow.Api/Wallow.Api.csproj
RUN dotnet build src/Wallow.Api/Wallow.Api.csproj -c Release --no-restore

# ============================================
# Stage 2: Build Migration Bundles
# ============================================
FROM build AS bundles
WORKDIR /src

# Install EF Core tools
RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"

# Build migration bundles for all modules
RUN mkdir -p /bundles && \
    for module_ctx in \
        "Identity:IdentityDbContext" \
        "Billing:BillingDbContext" \
        "Storage:StorageDbContext" \
        "Notifications:NotificationsDbContext" \
        "Messaging:MessagingDbContext" \
        "Announcements:AnnouncementsDbContext" \
        "ApiKeys:ApiKeysDbContext" \
        "Branding:BrandingDbContext" \
        "Inquiries:InquiriesDbContext"; do \
        module=$(echo "$module_ctx" | cut -d: -f1) && \
        ctx=$(echo "$module_ctx" | cut -d: -f2) && \
        lower=$(echo "$module" | tr '[:upper:]' '[:lower:]') && \
        echo "Building migration bundle for $module ($ctx)..." && \
        dotnet ef migrations bundle \
            --project "src/Modules/$module/Wallow.$module.Infrastructure/Wallow.$module.Infrastructure.csproj" \
            --startup-project src/Wallow.Api/Wallow.Api.csproj \
            --context "$ctx" \
            --output "/bundles/efbundle-$lower" \
            --configuration Release \
            --force \
            --no-build || exit 1; \
    done && \
    echo "Building Audit migration bundle..." && \
    dotnet ef migrations bundle \
        --project src/Shared/Wallow.Shared.Infrastructure.Core/Wallow.Shared.Infrastructure.Core.csproj \
        --startup-project src/Wallow.Api/Wallow.Api.csproj \
        --context AuditDbContext \
        --output /bundles/efbundle-audit \
        --configuration Release \
        --force \
        --no-build && \
    echo "Building AuthAudit migration bundle..." && \
    dotnet ef migrations bundle \
        --project src/Shared/Wallow.Shared.Infrastructure.Core/Wallow.Shared.Infrastructure.Core.csproj \
        --startup-project src/Wallow.Api/Wallow.Api.csproj \
        --context AuthAuditDbContext \
        --output /bundles/efbundle-authaudit \
        --configuration Release \
        --force \
        --no-build

# ============================================
# Stage 3: Publish API
# ============================================
FROM build AS publish
WORKDIR /src
RUN dotnet publish src/Wallow.Api/Wallow.Api.csproj -c Release -o /app/publish --no-build

# ============================================
# Stage 4: Runtime
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Install openssl for cert generation
RUN apt-get update && apt-get install -y --no-install-recommends openssl wget && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy published app
COPY --from=publish /app/publish .

# Copy migration bundles
COPY --from=bundles /bundles/ /app/bundles/

# Copy branding.json for configuration
COPY branding.json /app/branding.json

# Copy entrypoint
COPY docker/images/api/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["/entrypoint.sh"]
