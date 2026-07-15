# syntax=docker/dockerfile:1.9-labs
FROM node:22-alpine AS theme
WORKDIR /build
COPY api/branding.json scripts/generate-docs-theme.mjs ./
RUN node generate-docs-theme.mjs && \
    mv docfx/templates/wallow/public/main.css /main.css

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /build
COPY api/global.json ./
COPY api/Directory.Build.props api/Directory.Packages.props api/Wallow.slnx api/
COPY --parents api/src/**/*.csproj ./
COPY --parents api/tests/**/*.csproj ./
RUN dotnet restore api/Wallow.slnx
COPY api/src/ api/src/
COPY docs/ docs/
COPY docfx.json ./
COPY docfx/ docfx/
COPY .config/ .config/
COPY --from=theme /main.css docfx/templates/wallow/public/main.css
RUN dotnet tool restore && dotnet docfx docfx.json

FROM nginx:alpine
COPY docker/docs/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /build/.docfx/_site /usr/share/nginx/html
EXPOSE 80
