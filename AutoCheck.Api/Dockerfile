# AutoCheck academic prototype, SRS 1.1. SDK and runtime match global.json.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY AutoCheck.Api/AutoCheck.Api.csproj AutoCheck.Api/packages.lock.json AutoCheck.Api/
RUN dotnet restore "AutoCheck.Api/AutoCheck.Api.csproj" --locked-mode
COPY . .
WORKDIR "/src/AutoCheck.Api"
RUN dotnet build "AutoCheck.Api.csproj" -c $BUILD_CONFIGURATION --no-restore -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "AutoCheck.Api.csproj" -c $BUILD_CONFIGURATION --no-restore -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "AutoCheck.Api.dll"]
