# Consulte https://aka.ms/customizecontainer para aprender a personalizar su contenedor de depuración y cómo Visual Studio usa este Dockerfile para compilar sus imágenes para una depuración más rápida.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

# Esta fase se usa para compilar el proyecto de servicio
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["WebApi.MinimalAPI/WebApi.MinimalAPI.csproj", "WebApi.MinimalAPI/"]
RUN dotnet restore "./WebApi.MinimalAPI/WebApi.MinimalAPI.csproj"
COPY . .
WORKDIR "/src/WebApi.MinimalAPI"
RUN dotnet build "./WebApi.MinimalAPI.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Esta fase se usa para publicar el proyecto de servicio que se copiará en la fase final.
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./WebApi.MinimalAPI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=true

# Esta fase se usa en producción o cuando se ejecuta desde VS en modo normal (valor predeterminado cuando no se usa la configuración de depuración)
FROM base AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["./WebApi.MinimalAPI"]
