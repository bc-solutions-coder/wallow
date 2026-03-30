# ============================================
# Stage 1: Restore + Build
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Packages.props Directory.Build.props .editorconfig branding.json ./
COPY src/ src/

RUN dotnet restore src/Wallow.Auth/Wallow.Auth.csproj
RUN dotnet build src/Wallow.Auth/Wallow.Auth.csproj -c Release --no-restore

# ============================================
# Stage 2: Tailwind CSS
# ============================================
FROM build AS tailwind
WORKDIR /src/src/Wallow.Auth

# Download Tailwind CSS standalone binary for the target architecture
ARG TARGETARCH
RUN set -eux; \
    case "${TARGETARCH}" in \
        arm64) TW_ARCH="arm64" ;; \
        *)     TW_ARCH="x64"   ;; \
    esac; \
    curl -fsSL "https://github.com/tailwindlabs/tailwindcss/releases/latest/download/tailwindcss-linux-${TW_ARCH}" \
        -o /usr/local/bin/tailwindcss; \
    chmod +x /usr/local/bin/tailwindcss

# Build minified CSS
RUN tailwindcss -i Styles/app.css -o wwwroot/css/app.css --minify

# ============================================
# Stage 3: Publish
# ============================================
FROM build AS publish
WORKDIR /src

# Copy the Tailwind-built CSS into the project before publish
COPY --from=tailwind /src/src/Wallow.Auth/wwwroot/css/app.css src/Wallow.Auth/wwwroot/css/app.css

RUN dotnet publish src/Wallow.Auth/Wallow.Auth.csproj -c Release -o /app/publish --no-build

# ============================================
# Stage 4: Runtime
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime


WORKDIR /app

COPY --from=publish /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Wallow.Auth.dll"]
