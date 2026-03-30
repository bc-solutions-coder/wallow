# ============================================
# Stage 1: Restore + Build
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY .editorconfig Directory.Packages.props Directory.Build.props ./
COPY src/ src/

RUN dotnet restore src/Wallow.Web/Wallow.Web.csproj
RUN dotnet build src/Wallow.Web/Wallow.Web.csproj -c Release --no-restore

# ============================================
# Stage 2: Tailwind CSS
# ============================================
FROM build AS tailwind
WORKDIR /src/src/Wallow.Web

# Download Tailwind CSS standalone binary — select arch-appropriate variant
ARG TARGETARCH
RUN set -eux; \
    case "${TARGETARCH}" in \
        amd64) TAILWIND_ARCH="x64" ;; \
        arm64) TAILWIND_ARCH="arm64" ;; \
        *) echo "Unsupported architecture: ${TARGETARCH}" && exit 1 ;; \
    esac; \
    curl -fsSL "https://github.com/tailwindlabs/tailwindcss/releases/latest/download/tailwindcss-linux-${TAILWIND_ARCH}" \
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
COPY --from=tailwind /src/src/Wallow.Web/wwwroot/css/app.css src/Wallow.Web/wwwroot/css/app.css

RUN dotnet publish src/Wallow.Web/Wallow.Web.csproj -c Release -o /app/publish --no-build

# ============================================
# Stage 4: Runtime
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime


WORKDIR /app

COPY --from=publish /app/publish .

# Web does NOT reference branding.json in .csproj — must copy explicitly
COPY branding.json /app/branding.json

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Wallow.Web.dll"]
