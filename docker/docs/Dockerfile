# syntax=docker/dockerfile:1.9-labs
FROM node:22-alpine AS theme
WORKDIR /build
COPY branding.json scripts/generate-docs-theme.mjs ./
RUN mkdir -p templates/wallow/public && \
    node generate-docs-theme.mjs && \
    mv templates/wallow/public/main.css /main.css

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /build
COPY global.json Directory.Build.props Directory.Packages.props Wallow.slnx ./
COPY --parents src/**/*.csproj ./
COPY --parents tests/**/*.csproj ./
RUN dotnet restore Wallow.slnx
COPY src/ src/
COPY docs/ docs/
COPY docfx.json filterConfig.yml ./
COPY templates/ templates/
COPY .config/ .config/
COPY --from=theme /main.css templates/wallow/public/main.css
RUN dotnet tool restore && dotnet docfx docfx.json

FROM nginx:alpine
COPY docker/docs/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /build/.docfx/_site /usr/share/nginx/html
EXPOSE 80
