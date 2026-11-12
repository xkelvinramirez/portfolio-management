---
type: verification
title: Verification — maf-tool-calling-agent — 2026-11-11 — T017–T018 (pre-commit)
description: Verificación de alcance de tarea antes del commit de PendingActionExecutor y de los arreglos de la revisión de T013–T016. No es el veredicto de la feature.
tags: [sdd, verification]
timestamp: 2026-11-12T05:40:00Z
topic: sdd
slug: maf-tool-calling-agent
source_state: 251f623 on feature/maf-tool-calling-agent-pr3 (dirty — the T017–T018 changes under verification)
---

# Verification — maf-tool-calling-agent — 2026-11-11 — T017–T018

## Stack gate (config.yaml; ejecución nueva tras la última edición)
- format `--verify-no-changes` → PASS (exit 0)
- build → PASS (0 avisos, 0 errores)
- unit → PASS — 272/272
- integration → PASS — 11/11

## Task done-checks
- [x] T017 — `PendingActionExecutorTests` rojo (CS0246: `PendingActionExecutor` ausente). Cubre: cada una de las 8 operaciones despacha exactamente su comando de plan §3 con los valores del payload; refresh devuelve `Updated` y `Skipped`; `CreatePortfolio`/`CreateEntry` devuelven el id de `IAddedEntityIdLog` aunque la respuesta traiga `Id=0`; nota inactiva → `IsActive=false`; despacho con `CancellationToken.None` aunque llegue un token vivo; error del comando → error `ErrorOr`; creación con éxito sin id registrado → `InvalidOperationException` (error de cableado).
- [x] T018 — T017 verde, 11/11.

## Arreglos de la revisión de T013–T016 (VERDICT pass)
- #2 (important): `FieldSnapshotTests` fija que fechas `Utc`, `Unspecified`, `Local` y `DateTimeOffset` del mismo instante dan el mismo snapshot.
- #4 (minor): un título con forma de fecha en `Before` se muestra sin tocar.
- #3 (minor): `StaleCheck` documenta por qué basta con recorrer `before`.

## Control de los gates propios (mutantes)
- `Unspecified` desplazado con `ToUniversalTime` → 1 fallo · guarda de fecha por contenido → 1 fallo · el ejecutor reenvía `ct` → 1 fallo · id de la entrada tomado de la respuesta → 1 fallo · nota siempre activa → 1 fallo · restaurado e idéntico → 0 fallos.

## Capas no ejecutadas
- **SUSTITUIDA** — pipeline real de los comandos internos: `ISender` es un doble. No detecta errores de cableado de MediatR ni del commit anidado de la UoW; eso lo cubren T031 (HTTP) y T033 (Postgres, id > 0).

## Hallazgos descartados
- Revisión #1 ("decimales con distinta escala dan un falso 'cambia'"): "G29" ya quita los ceros finales. `CurrentValuesReaderTests` convierte `0.50m` en "0.5" y `60000.100m` en "60000.1", y el mutante "sin G29" falla 2 tests; `FieldSnapshotTests.Decimals_DropTrailingZeros…` lo fija.

## Verdict: PASS (alcance de tareas T017–T018). Listo para commit.
