FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /src
COPY api/Directory.Build.props api/Directory.Build.targets api/Directory.Packages.props api/.editorconfig api/
COPY api/src/Wallow.TelemetryGateway api/src/Wallow.TelemetryGateway
RUN dotnet publish api/src/Wallow.TelemetryGateway -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11
WORKDIR /app
COPY --from=build /out .
USER $APP_UID
ENTRYPOINT ["dotnet", "Wallow.TelemetryGateway.dll"]
