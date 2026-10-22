# syntax=docker/dockerfile:1.7
# ADR-008: Multi-stage production container for Carpediem ASP.NET Core 10 API.
# ADR-009: Containers never execute Database.Migrate() at startup; migrations run out-of-process.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props Carpediem.slnx ./
COPY src/Carpediem.Web/Carpediem.Web.csproj src/Carpediem.Web/
COPY src/Infrastructure/Carpediem.Infrastructure.csproj src/Infrastructure/
COPY src/Modules/Attendance/Carpediem.Modules.Attendance.csproj src/Modules/Attendance/
COPY src/Modules/Events/Carpediem.Modules.Events.csproj src/Modules/Events/
COPY src/Modules/Identity/Carpediem.Modules.Identity.csproj src/Modules/Identity/
COPY src/Modules/Notifications/Carpediem.Modules.Notifications.csproj src/Modules/Notifications/
COPY src/Modules/Staffing/Carpediem.Modules.Staffing.csproj src/Modules/Staffing/

RUN dotnet restore src/Carpediem.Web/Carpediem.Web.csproj

COPY src/ src/
RUN dotnet publish src/Carpediem.Web/Carpediem.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# ASVS V13.1: Run container process as non-root user and bind HTTP 8080 behind TLS-terminating proxy/ALB.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

COPY --from=build /app/publish ./

USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "Carpediem.Web.dll"]
