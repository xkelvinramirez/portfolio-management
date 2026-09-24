# Migración inicial de base de datos (PostgreSQL)

## Contexto

El proyecto tenía EF Core y Npgsql configurados, pero faltaban piezas para poder generar y aplicar
la primera migración: el modelo de datos estaba incompleto, la herramienta `dotnet-ef` estaba
desactualizada, y algunas credenciales vivían en texto plano en archivos versionados.

## Cambios en el modelo de datos (`src/Domain`, `src/Infrastructure`)

- **`CryptoCurrency` y `Exchange` no estaban en el modelo de EF**: no tenían `IEntityTypeConfiguration`
  ni eran referenciados por ninguna navegación, por lo que EF Core no los descubría y no se
  generaban sus tablas. Se agregaron:
  - `Infrastructure/CryptoCurrencies/CryptoCurrencyConfiguration.cs`
  - `Infrastructure/Exchanges/ExchangeConfiguration.cs`
- **Relación `Portfolio` → `PortfolioEntry` incompleta**: estaba comentada en
  `PortfolioConfiguration.cs`. Se creó `Infrastructure/Portfolios/PortfolioEntryConfiguration.cs`
  con las tres relaciones de `PortfolioEntry` (`Portfolio`, `CryptoCurrency`, `Exchange`), precisión
  decimal explícita (`numeric(28,18)`) para `Quantity`/`PricePerUnit`.
- **Navegaciones de proyección**: se agregaron las propiedades `CryptoCurrency` y `Exchange` en
  `Domain/Entities/PortfolioEntry.cs` para poder hacer `Include`/proyecciones sin joins manuales
  (ej. mostrar símbolo de la crypto y nombre del exchange en un listado de entries).
- **`AppDbContext`**: se agregaron los `DbSet<T>` de `User`, `Portfolio`, `PortfolioEntry`,
  `CryptoCurrency`, `Exchange` (antes solo se usaba `Set<T>()` genérico).
- **Consistencia de nombres de tabla**: `UserConfiguration` no tenía `ToTable(...)`, por lo que la
  convención snake_case generaba la tabla `users` en minúsculas mientras el resto quedaba en
  PascalCase (`Portfolios`, `Exchanges`, etc.). Se agregó `builder.ToTable("Users")`.

## Bugs de arranque encontrados y corregidos (`src/Infrastructure`)

Estos rompían la validación del contenedor de DI en `Development` (`builder.Build()` falla si
`ValidateOnBuild` está activo), no solo la generación de la migración:

- `AddHealthChecksUI()` no tenía backend de almacenamiento configurado
  (`IHealthCheckFailureNotifier` no podía resolver `HealthChecksDb`). Se agregó
  `.AddInMemoryStorage()` y el paquete `AspNetCore.HealthChecks.UI.InMemory.Storage`.
- `appsettings.Development.json` declaraba el sink `Serilog.Sinks.Seq` en `Serilog:Using` sin tener
  el paquete referenciado. Se agregó `Serilog.Sinks.Seq` a `Infrastructure.csproj`.
- El paquete de storage anterior trae `Microsoft.EntityFrameworkCore.InMemory 8.0.11` transitivo,
  en conflicto binario con EF Core 10.0.12 usado en el resto del proyecto (`MissingMethodException`
  al ejecutar `dotnet ef`). Se fijó explícitamente en `10.0.12`.

## Herramientas necesarias para migraciones

- `dotnet-ef` global actualizado de `8.0.6` → `10.0.12` (debe ser >= a la versión de
  `Microsoft.EntityFrameworkCore` del proyecto).
- Se agregó `Microsoft.EntityFrameworkCore.Design` al proyecto de arranque
  (`WebApi.MinimalAPI.csproj`), requerido por las herramientas de EF (antes solo estaba en
  `Infrastructure.csproj` con `PrivateAssets="all"`, que no fluye transitivamente).

## Secretos movidos a `dotnet user-secrets`

Se encontraron credenciales reales en archivos versionados. Se movieron al secret store local
(`UserSecretsId` agregado a `WebApi.MinimalAPI.csproj`):

| Clave | Origen anterior |
|---|---|
| `ConnectionStrings:Database` | `appsettings.Development.json` (password de Supabase en texto plano) |
| `Jwt:Secret` | `appsettings.Development.json` |
| `MEDIATR_LICENSE_KEY` | `Properties/launchSettings.json` (perfil `http`) |

**Cada desarrollador debe configurar sus propios secretos localmente**, ya que `secrets.json` vive
fuera del repo (`%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json` en Windows):

```bash
dotnet user-secrets set "ConnectionStrings:Database" "<cadena de conexión>" --project src/WebApi.MinimalAPI
dotnet user-secrets set "Jwt:Secret" "<secreto>" --project src/WebApi.MinimalAPI
dotnet user-secrets set "MEDIATR_LICENSE_KEY" "<license key>" --project src/WebApi.MinimalAPI
```

> Pendiente para despliegue (staging/prod): estas claves no existen en `appsettings.json` base ni
> en user secrets (son solo locales). Hace falta definirlas como variables de entorno
> (`ConnectionStrings__Database`, etc.) o un secret manager en el pipeline/host de destino. El
> perfil `Container (Dockerfile)` de `launchSettings.json` tampoco las tiene.

## Migración generada y aplicada

- `src/Infrastructure/Common/Persistence/Migrations/*_InitialCreate.cs` (+ `Designer.cs` y
  `AppDbContextModelSnapshot.cs`): crea `Users`, `Portfolios`, `PortfolioEntries`,
  `CryptoCurrencies`, `Exchanges` con FKs, índices únicos (`email`, `symbol`, `name`) e índices por
  FK.
- La migración ya se aplicó contra la base de Supabase configurada en `ConnectionStrings:Database`
  (`dotnet ef database update`).

### Comandos de referencia

```bash
# Generar una nueva migración
dotnet ef migrations add <Nombre> --project src/Infrastructure --startup-project src/WebApi.MinimalAPI --context AppDbContext

# Aplicar migraciones pendientes
dotnet ef database update --project src/Infrastructure --startup-project src/WebApi.MinimalAPI --context AppDbContext
```
