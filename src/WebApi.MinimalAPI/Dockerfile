# Consulte https://aka.ms/customizecontainer para aprender a personalizar su contenedor de depuración y cómo Visual Studio usa este Dockerfile para compilar sus imágenes para una depuración más rápida.

# Estos ARG permiten intercambiar la base usada para crear la imagen final al depurar desde VS
ARG LAUNCHING_FROM_VS
# Esto establece la imagen base para el final, pero solo si se ha definido LAUNCHING_FROM_VS
ARG FINAL_BASE_IMAGE=${LAUNCHING_FROM_VS:+aotdebug}

# Esta fase se usa cuando se ejecuta desde VS en modo rápido (valor predeterminado para la configuración de depuración)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080


# Esta fase se usa para compilar el proyecto de servicio
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# Instalación de las dependencias clang/zlib1g-dev para publicar en nativo
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
    clang zlib1g-dev
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

# Esta fase se usa como base para la fase final al iniciar desde VS para admitir la depuración en modo normal (valor predeterminado cuando no se usa la configuración de depuración)
FROM base AS aotdebug
USER root
# Instalación de GDB para admitir la depuración nativa
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
    gdb
USER app

# Esta fase se usa en producción o cuando se ejecuta desde VS en modo normal (valor predeterminado cuando no se usa la configuración de depuración)
FROM ${FINAL_BASE_IMAGE:-mcr.microsoft.com/dotnet/runtime-deps:10.0} AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["./WebApi.MinimalAPI"]