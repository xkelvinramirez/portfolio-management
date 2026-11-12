---
type: verification
title: Verification — maf-tool-calling-agent — 2026-11-12 — T023–T024 (pre-commit)
description: Verificación de alcance de tarea antes del commit de SendMafAgentMessageCommand y del registro DI de Application. No es el veredicto de la feature.
tags: [sdd, verification]
timestamp: 2026-11-12T10:00:00Z
topic: sdd
slug: maf-tool-calling-agent
source_state: 9ff049b on feature/maf-tool-calling-agent-pr3 (dirty — the T023–T024 changes under verification)
---

# Verification — maf-tool-calling-agent — 2026-11-12 — T023–T024

## Stack gate (config.yaml; ejecución nueva tras la última edición)
- format `--verify-no-changes` → PASS (exit 0)
- build → PASS (0 avisos, 0 errores)
- unit → PASS — 336/336
- integration → PASS — 11/11

## Task done-checks
- [x] T023 — `SendMafAgentMessageCommandHandlerTests` rojo (CS0246: handler ausente). 20 tests: `ConversationId` null → conversación nueva del usuario, el runner recibe sesión vacía, se prepara con `Add` solo tras un turno correcto (un turno fallido no la prepara); conversación existente → el runner recibe la sesión descifrada y la nueva se guarda cifrada con `Update`; `LastActivityAt` = ahora; inexistente y de otro usuario → el mismo `Conversation.NotFound` sin runner ni descifrado; más de 24 h → `Conversation.Expired` sin runner (24 h exactas sigue válida); sesión corrupta → excepción (500); los `PendingActionDto` salen de `MafAgentTurnResult.ProposedActions` vía `PendingActionFactory.ToDto`, en orden y solo los del turno; `AgentLoopExhaustedException`/`AgentTimeoutException` → `MafAgent.LoopExhausted`/`MafAgent.Timeout` (Failure), sin `Update`, aviso en log sin el texto del mensaje; otras excepciones no se tragan; el handler no depende de `PendingActionExecutor`, `ISender` ni `IMediator`; el comando es `ICommand`; validador 1..2000 y rechaza solo espacios.
- [x] T024 — T023 verde; validador `public sealed`; `MafAgentApplicationDiTests` verde (4): el `AddApplication(new ConfigurationManager())` real con dobles de los puertos resuelve `PendingActionFactory` (scoped: misma instancia en el ámbito, distinta entre ámbitos), `ICurrentValuesReader → CurrentValuesReader` y `PendingActionExecutor`; resuelve los handlers de Send, Confirm y Reject; el `IValidator<SendMafAgentMessageCommand>` registrado rechaza 2001 caracteres; los validadores de Confirm y Reject están registrados.

## Control de los gates propios (mutantes)
- 13 mutantes, todos detectados: sin dueño (1) · sin caducidad (1) · sesión sin cifrar (2) · sin descifrar (2) · sin `Update` (1) · LoopExhausted→Timeout (1) · runner sin token (2) · pierde acciones propuestas (1) · otra hora en la sesión (2) · límite 2001 (2) · mensaje vacío permitido (2) · lector no registrado (2) · factoría singleton (2). Ficheros restaurados (el BOM de `DependencyInjection.cs` se repuso tras el mutante).

## Capas no ejecutadas
- **SUSTITUIDA** — commit real de conversación y acciones juntos, y "nada queda tras un turno fallido" (AC14) con la UoW real: aquí con repositorios dobles. Lo cubren el test HTTP de T031 y el de reinicio de T033.

## Hallazgos descartados
- ninguno

## Verdict: PASS (alcance de tareas T023–T024). Listo para commit.
