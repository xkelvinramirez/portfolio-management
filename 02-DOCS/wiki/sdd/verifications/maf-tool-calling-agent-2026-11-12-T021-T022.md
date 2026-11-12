---
type: verification
title: Verification — maf-tool-calling-agent — 2026-11-12 — T021–T022 (pre-commit)
description: Verificación de alcance de tarea antes del commit de RejectPendingActionCommand. No es el veredicto de la feature.
tags: [sdd, verification]
timestamp: 2026-11-12T09:00:00Z
topic: sdd
slug: maf-tool-calling-agent
source_state: cc88155 on feature/maf-tool-calling-agent-pr3 (dirty — the T021–T022 changes under verification)
---

# Verification — maf-tool-calling-agent — 2026-11-12 — T021–T022

## Stack gate (config.yaml; ejecución nueva tras la última edición)
- format `--verify-no-changes` → PASS (exit 0)
- build → PASS (0 avisos, 0 errores)
- unit → PASS — 311/311
- integration → PASS — 11/11

## Task done-checks
- [x] T021 — `RejectPendingActionCommandHandlerTests` rojo (CS0246: comando, handler y validador ausentes). 15 tests: `Pending` → éxito **mediante `TryRejectAsync(id, userId, now, token)`**, sin `Update` y con la entidad cargada intacta (`Pending`, sin `DecidedAt`); `TryRejectAsync=false` → `AlreadyDecided`; inexistente, de otro usuario, acción ajena dentro de la conversación propia y conversación ausente → el mismo `NotFound`; acción caducada y conversación con más de 24 h → `Expired`; ya decidida (5 estados) → `AlreadyDecided` sin intentar la transición; el comando no es `ICommand`; validador con `Id` no vacío. 0 despachos a `ISender`: el handler no recibe `ISender`.
- [x] T022 — T021 verde (15/15).

## Control de los gates propios (mutantes)
- sin comprobación previa de `Pending` → 5 fallos · sin caducidad de la conversación → 1 · sin caducidad de la acción → 1 · `MarkRejected` + `Update` de la entidad → 1 · `CancellationToken.None` en `TryRejectAsync` → 1 · conversación nula aceptada → 1 · otra hora en `TryRejectAsync` → 1 · sin dueño de la acción → 0 al principio; tras añadir el test de acción ajena en conversación propia → 1. Fichero restaurado e idéntico (hash).

## Capas no ejecutadas
- **SUSTITUIDA** — atomicidad real de `TryRejectAsync` y su carrera con `TryClaimAsync`: aquí con repositorio doble. Está probada contra Postgres en T011/T012; el 204 por HTTP lo prueba T031.

## Hallazgos descartados
- ninguno

## Verdict: PASS (alcance de tareas T021–T022). Listo para commit.
