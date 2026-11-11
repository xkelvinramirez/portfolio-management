---
type: verification
title: Verification — maf-tool-calling-agent — 2026-11-11 — T002 (pre-commit)
description: Verificación de alcance de tarea antes del commit de la infraestructura de tests HTTP del agente MAF. No es el veredicto de la feature.
tags: [sdd, verification]
timestamp: 2026-11-12T03:30:00Z
topic: sdd
slug: maf-tool-calling-agent
source_state: 1a569f3 on feature/maf-tool-calling-agent-pr2 (dirty — the T002 changes under verification)
---

# Verification — maf-tool-calling-agent — 2026-11-11 — T002

## Stack gate (config.yaml; ejecución nueva tras la última edición, incluido el `dotnet format`)
- format `--verify-no-changes` → PASS (exit 0)
- build → PASS (0 avisos, 0 errores)
- unit → PASS — 197/197 (193 + 4 `MafAgentApiFactorySmokeTests`)
- integration → PASS — 11/11

## Task done-check T002
- [x] `WebApi.MinimalAPI.Tests` referencia `Microsoft.AspNetCore.Mvc.Testing` 10.0.14, `Microsoft.Extensions.TimeProvider.Testing` 10.11.0 y `Microsoft.EntityFrameworkCore.InMemory` 10.0.14.
- [x] Entorno `Testing` sin user-secrets; configuración en memoria para Jwt, Encryption:Key (32 bytes), cadenas de conexión, CosmosDb, CoinGecko y Ai; `ApplicationInsights:ConnectionString` vacío.
- [x] Doble NSubstitute de `CosmosClient`: `CreateDatabaseIfNotExistsAsync` lanza `CosmosException`, que `EnsureCosmosDbInitializedAsync` captura y registra como aviso. El host arranca.
- [x] Autenticación de test (claim `sub` desde la cabecera `X-Test-UserId`); `IMafAgentRunner`, `IEmailSender` e `ICoinGeckoClient` son dobles (ningún test envía emails ni llama a servicios externos); `FakeTimeProvider`.
- [x] EF InMemory con el interceptor `AddedEntityIdLog` añadido de nuevo (L3); `IPendingActionRepository` de test con transiciones condicionales en memoria bajo `lock` en un contexto aislado, incluido `TryMarkFailedAsync` (L2).
- [x] `MafAgentApiFactorySmokeTests` 4/4: 200 con identidad, 401 sin ella, dobles cableados, transiciones del doble.
- Producción: solo `InternalsVisibleTo(WebApi.MinimalAPI.Tests)` en `WebApi.MinimalAPI.csproj`, para que `WebApplicationFactory<Program>` vea el `Program` interno. Sin cambios de código.

## Control de los gates propios
- Mutante "repositorio real (ExecuteUpdate) en lugar del doble" → 2 fallos (cableado y transiciones).
- Mutante "sin doble de Cosmos" → 4/4 fallan (el host no arranca).
- Hallazgo: el `HostedService` de HealthChecks UI fallaba en el host de test (no carga `IdentityModel 5.2`) y sondeaba `/health`. Se retira solo ese hosted service en la factoría, filtrando por espacio de nombres; el servidor web no se toca. Ruido tras el cambio: 0 líneas.

## Capas no ejecutadas
- **SUSTITUIDA** — atomicidad de las transiciones en tests HTTP: el doble serializa con `lock` en proceso. No prueba la atomicidad real; esa la prueban los tests de integración contra Postgres (T011/T012).

## Hallazgos descartados
- ninguno

## Fuera de alcance, para el usuario
- `HealthChecks.UI` no carga `IdentityModel, Version=5.2.0.0` en la salida de tests. Conviene comprobar si pasa también en la API desplegada (`/healthchecks-ui`). Es preexistente y no está relacionado con esta feature.

## Verdict: PASS (alcance de tarea T002). Listo para commit. Con esto se completa el PR2 (T002–T012, T037, T038).
