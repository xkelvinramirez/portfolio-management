---
type: verification
title: Verification — maf-tool-calling-agent — 2026-11-12 — T033 (pre-commit)
description: Verificación de alcance de tarea antes del commit del test de reinicio contra Postgres, y de los arreglos de la revisión de T031–T032. No es el veredicto de la feature.
tags: [sdd, verification]
timestamp: 2026-11-12T18:30:00Z
topic: sdd
slug: maf-tool-calling-agent
source_state: 99ea931 on feature/maf-tool-calling-agent-pr5 (dirty — the T033 changes under verification)
---

# Verification — maf-tool-calling-agent — 2026-11-12 — T033

## Stack gate (config.yaml; ejecución nueva tras la última edición)
- format `--verify-no-changes` → PASS (exit 0)
- build → PASS (0 avisos, 0 errores)
- unit → PASS — 469/469
- integration → PASS — 12/12 (11 anteriores + `MafAgentRestartTests`, contra el Postgres de desarrollo)

## Task done-checks
- [x] T033 — `MafAgentRestartTests` (Category=Integration) verde contra Postgres. Todo es real salvo el modelo (un `IChatClient` con guion) y los sistemas externos: runner MAF, herramientas, sesión cifrada, repositorios con el reclamo atómico `ExecuteUpdate` e interceptor `AddedEntityIdLog`.
  - Un primer host propone una entrada desde el chat (símbolo en minúsculas y exchange en mayúsculas, fecha "2026-11-10"). En Postgres quedan la conversación del usuario con la sesión cifrada, la acción `Pending` con el payload cifrado, y ninguna entrada (AC2).
  - Un SEGUNDO host, un `ServiceProvider` nuevo como tras un reinicio, continúa la conversación: el modelo recibe la pregunta y la respuesta del primer turno (AC15).
  - Ese segundo host confirma la acción: 200 `Succeeded` con un id > 0, igual al id de la fila que Postgres insertó (AC3). La entrada tiene el activo, el exchange, la cantidad y la fecha `2026-11-10T00:00:00Z` (decisión del usuario: fecha sin hora = día UTC), y la acción queda `Succeeded`.
- Control: el test falla si la sesión no se restaura y si el resultado no lleva el id real (2 mutantes, ambos detectados). Limpieza: `DisposeAsync` borra el usuario de prueba (en cascada) y las filas de catálogo creadas.

## Arreglos de la revisión de T031–T032 (PASS, sin importantes)
- minor #1: test de que `confirm` llega al pipeline con un token NO cancelable (behavior de MediatR que lo registra); con el token de la petición falla.
- minor #2: rechazar dos veces → 409 `AlreadyDecided`; id inexistente → 404.
- minor #4: el doble del runner se reinicia en cada test (`ClearSubstitute(ReturnValues)`), así un `ThrowsAsync` no pasa a otro test.
- minor #3 aceptado: el test de AC4 por HTTP solo prueba que el camino del chat no confirma nada por sí mismo; que el modelo no pueda confirmar lo prueban las herramientas (no hay herramienta de confirmar).
- `MafAgentEndpointsTests`: 20/20.

## Capas no ejecutadas
- **SUSTITUIDA** — modelo real: guion. La checklist manual con Ollama es T035.

## Hallazgos descartados
- ninguno

## Verdict: PASS (alcance de tarea T033 y arreglos de revisión). Listo para commit.
