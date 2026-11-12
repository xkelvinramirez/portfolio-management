---
type: verification
title: Verification — maf-tool-calling-agent — 2026-11-11 — T019–T020 (pre-commit)
description: Verificación de alcance de tarea antes del commit de ConfirmPendingActionCommand. No es el veredicto de la feature.
tags: [sdd, verification]
timestamp: 2026-11-12T06:30:00Z
topic: sdd
slug: maf-tool-calling-agent
source_state: 984bb1f on feature/maf-tool-calling-agent-pr3 (dirty — the T019–T020 changes under verification)
---

# Verification — maf-tool-calling-agent — 2026-11-11 — T019–T020

## Stack gate (config.yaml; ejecución nueva tras la última edición)
- format `--verify-no-changes` → PASS (exit 0)
- build → PASS (0 avisos, 0 errores)
- unit → PASS — 291/291
- integration → PASS — 11/11

## Task done-checks
- [x] T019 — `ConfirmPendingActionCommandHandlerTests` rojo (CS0234/CS0246: `Application.MafAgent.Command` y el handler ausentes). 19 tests: caso feliz → `Succeeded` con resultado (id 42 de `IAddedEntityIdLog`), `DecidedAt` y `ResultJson` cifrado; inexistente y de otro usuario → el mismo `PendingAction.NotFound`, sin reclamo; acción caducada y conversación con más de 24 h → `Expired`; ya decidida o reclamo perdido → `AlreadyDecided`; edición con desfase → valor `Stale`, `MarkStale`, 0 despachos; recurso borrado → valor `Failed`; fallo del comando → valor `Failed` con su código; excepción del comando → `DiscardAll()` antes de `TryMarkFailedAsync(id, "MafAgent.PendingAction.UnexpectedFailure", now)`, valor `Failed`, sin nota de sesión, sin `Update` y con log de error; payload que no se descifra tras el reclamo → valor `Failed`; durante el comando la acción está en `Executing`; lectura de desfase y comando con `CancellationToken.None`; otra acción en la misma conversación no impide confirmar; nota de resultado sin texto libre añadida a la sesión cifrada.
- [x] T020 — T019 verde (19/19); validador `public sealed` (`Id` no vacío); comando `ICommand`.

## Control de los gates propios (mutantes)
- sin `DiscardAll` → 2 fallos · sin `MarkExecuting` → 9 · sin comprobar el desfase → 1 · sin caducidad de la conversación → 1 · token de la petición en la lectura → 1 · sin la comprobación previa de estado → 1 · sin nota de sesión → 2 · restaurado e idéntico → 0.
- **Superviviente equivalente:** quitar la comprobación de dueño de la acción. La de la conversación devuelve el mismo `NotFound`, y una acción y su conversación siempre tienen el mismo dueño (`PendingActionFactory` toma ambos de `ICurrentUserProvider`). AC7 sigue cubierto.

## Capas no ejecutadas
- **SUSTITUIDA** — atomicidad del reclamo y commit real: repositorios dobles. El reclamo atómico está probado contra Postgres (T011/T012); el flujo completo HTTP lo prueban T031 y T033.

## Hallazgos descartados
- ninguno

## Verdict: PASS (alcance de tareas T019–T020). Listo para commit.
