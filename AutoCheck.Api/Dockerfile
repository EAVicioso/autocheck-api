# Etapa de ejecución con el runtime de .NET 10 parcheado
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Etapa de compilación con el SDK 10.0.14
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["AutoCheck.Api/AutoCheck.Api.csproj", "AutoCheck.Api/"]
RUN dotnet restore "AutoCheck.Api/AutoCheck.Api.csproj"
COPY . .
WORKDIR "/src/AutoCheck.Api"
RUN dotnet build "AutoCheck.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "AutoCheck.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AutoCheck.Api.dll"]