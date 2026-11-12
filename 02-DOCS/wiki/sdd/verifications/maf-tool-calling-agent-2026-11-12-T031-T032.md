---
type: verification
title: Verification — maf-tool-calling-agent — 2026-11-12 — T031–T032 (pre-commit)
description: Verificación de alcance de tarea antes del commit de los endpoints HTTP del agente MAF. No es el veredicto de la feature.
tags: [sdd, verification]
timestamp: 2026-11-12T17:00:00Z
topic: sdd
slug: maf-tool-calling-agent
source_state: e2cba62 on feature/maf-tool-calling-agent-pr5 (dirty — the T031–T032 changes under verification)
---

# Verification — maf-tool-calling-agent — 2026-11-12 — T031–T032

## Stack gate (config.yaml; ejecución nueva tras la última edición)
- format `--verify-no-changes` → PASS (exit 0)
- build → PASS (0 avisos, 0 errores)
- unit → PASS — 467/467
- integration → PASS — 11/11

## Task done-checks
- [x] T031 — `MafAgentEndpointsTests` rojo: 17 de 18 fallan con 404 sin cuerpo porque las rutas no existen. 18 tests con `MafAgentApiFactory`, un id de usuario por test y `Runner.ClearReceivedCalls()`; el reloj compartido no se mueve:
  - `chat` 200 con `conversationId`, `reply` y la acción del turno; la conversación queda guardada con su dueño;
  - un chat con "sí", "confirmo" o el id escrito no ejecuta nada: la acción sigue `Pending` y no hay portafolio (AC4);
  - conversación ajena → 404 `MafAgent.Conversation.NotFound` sin llamar al agente (AC13); más de 24 h → 409 `Expired`; mensaje vacío o de 2001 → 400; límite de pasos → 400 `MafAgent.LoopExhausted` y no se guarda la conversación (AC14);
  - confirmar una `CreateEntry` → 200 `Succeeded` con el id real de la fila insertada (AC3, mediante `AddedEntityIdLog` en InMemory);
  - id inexistente y acción ajena → el mismo 404 `MafAgent.PendingAction.NotFound`, y la ajena sigue `Pending` (AC7);
  - confirmar dos veces → 200 y luego 409 `AlreadyDecided`, con un único portafolio (AC5); acción caducada → 409 `Expired` sin escribir (AC6);
  - rechazar → 204, estado `Rejected`, y confirmar después → 409 sin escribir; rechazar una acción ajena → 404;
  - sin identidad → 401 en los tres endpoints.
- [x] T032 — T031 verde (18/18). `Select-String MafAgent src/WebApi.MinimalAPI/Program.cs` → 0 coincidencias. Registro en `EndpointRegistrar`, etiqueta `EndpointTags.MafAgent`.

## Control de los gates propios (mutantes)
- sin registrar los endpoints (17) · rechazar devuelve 200 (1) · sin `RequireAuthorization` (3) · rechazar llama a confirmar (1): todos detectados. Ficheros restaurados byte a byte.

## Capas no ejecutadas
- **SUSTITUIDA** — `confirm` sin token de cancelación (pauta de la revisión T017–T020): se envía `ConfirmPendingActionCommand` sin token, como todos los endpoints actuales. Un test HTTP no puede provocar una desconexión justo tras el reclamo, así que esto se verifica leyendo el código (`MafAgentEndpoints.cs`), no ejecutándolo.
- **SUSTITUIDA** — Postgres real y `ExecuteUpdate`: aquí EF InMemory con el doble de transiciones. Lo cubre T033 (integración).

## Hallazgos descartados
- ninguno

## Verdict: PASS (alcance de tareas T031–T032). Listo para commit.
