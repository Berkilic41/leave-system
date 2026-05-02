# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/LeaveSystem.Data/LeaveSystem.Data.csproj     src/LeaveSystem.Data/
COPY src/LeaveSystem.Bll/LeaveSystem.Bll.csproj       src/LeaveSystem.Bll/
COPY src/LeaveSystem.Web/LeaveSystem.Web.csproj       src/LeaveSystem.Web/
RUN dotnet restore src/LeaveSystem.Web/LeaveSystem.Web.csproj

COPY src/ src/
RUN dotnet publish src/LeaveSystem.Web/LeaveSystem.Web.csproj \
    --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN groupadd -r app && useradd -r -g app app
COPY --from=build /app/publish .
RUN chown -R app:app /app
USER app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "LeaveSystem.Web.dll"]
