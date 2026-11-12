---
type: verification
title: Verification — maf-tool-calling-agent — 2026-11-11 — T013–T016 (pre-commit)
description: Verificación de alcance de tarea antes del commit de PendingActionFactory, StaleCheck y CurrentValuesReader. No es el veredicto de la feature.
tags: [sdd, verification]
timestamp: 2026-11-12T05:00:00Z
topic: sdd
slug: maf-tool-calling-agent
source_state: f0f07c5 on feature/maf-tool-calling-agent-pr3 (dirty — the T013–T016 changes under verification)
---

# Verification — maf-tool-calling-agent — 2026-11-11 — T013–T016

## Stack gate (config.yaml; ejecución nueva tras la última edición, incluido el `dotnet format`)
- format `--verify-no-changes` → PASS (exit 0, tras formatear los 2 tests nuevos)
- build → PASS (0 avisos, 0 errores)
- unit → PASS — 253/253 (197 + 56 nuevos)
- integration → PASS — 11/11

## Task done-checks
- [x] T013 — `PendingActionFactoryTests` rojo antes de existir el código (CS0234/CS0246: `Payloads` y `PendingActionFactory` ausentes). Cubre: las 8 operaciones (valores exactos en el DTO, ida y vuelta del payload); en `Update*` el payload es la petición completa y `Before` debe tener exactamente sus campos; `ExpiresAt` +10 min con `FakeTimeProvider`; fechas de `Values` y `Before` en ISO `...Z`; `Summary` con hora de Madrid `+01:00` (noviembre) y `+02:00` (julio); payload y before cifrados.
- [x] T014 — T013 verde, 37/37.
- [x] T015 — `StaleCheckTests` y `CurrentValuesReaderTests` rojos (CS0246: `CurrentValuesReader` ausente). Cubren: snapshots iguales → sin desfase; cualquier campo distinto, nulo o ausente → desfase; despacho a `GetPortfolioByIdQuery` / `GetPortfolioEntryByIdQuery` / `GetNoteByIdQuery`; decimales "G29" y fechas "O" UTC; claves = campos de la petición completa (incl. `IsActive`, `CryptoCurrencyId`, `ExchangeId`); cambio externo en un campo no editado → desfase; NotFound se propaga.
- [x] T016 — T015 verde, 19/19.

## Control de los gates propios (mutantes)
- Factoría sin comprobar los campos de `Before` → 3 fallos.
- `Summary` marca todos los campos como cambiados → 1 fallo.
- `Summary` en UTC en vez de hora local → 2 fallos.
- `StaleCheck` ignora campos ausentes → 1 fallo.
- Lector sin `IsActive` → 2 fallos.
- Snapshot sin "G29" → 2 fallos.
- Código restaurado e idéntico (diff) → 0 fallos.

## Decisión de implementación (hueco del plan)
- El plan dice que el payload es "la petición tipada (`Update*Request`)", pero esas peticiones no llevan el id del recurso que el ejecutor necesita (`UpdatePortfolioCommand(long Id, …)`). Se crean registros de payload propios (`Application/MafAgent/Payloads`): el id objetivo marcado con `[PayloadTarget]` más todos los campos de la petición completa. `Before` es el `FieldSnapshot` (diccionario) que devuelve `ICurrentValuesReader`, así que ambos snapshots se comparan sin conversión. No cambia ninguna decisión del usuario.

## Capas no ejecutadas
- **NO-APLICA** — integración/HTTP: estas piezas son de Application y no tocan la base de datos; su uso real se cubre en T031/T033.

## Hallazgos descartados
- ninguno

## Verdict: PASS (alcance de tareas T013–T016). Listo para commit.
