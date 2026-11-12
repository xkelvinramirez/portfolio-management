---
type: verification
title: Verification — maf-tool-calling-agent — 2026-11-12 — T029–T030 (pre-commit)
description: Verificación de alcance de tarea antes del commit del runner MAF, la fábrica del cliente de chat y su registro en DI. No es el veredicto de la feature.
tags: [sdd, verification]
timestamp: 2026-11-12T15:00:00Z
topic: sdd
slug: maf-tool-calling-agent
source_state: 2f6725c on feature/maf-tool-calling-agent-pr4 (dirty — the T029–T030 changes under verification)
---

# Verification — maf-tool-calling-agent — 2026-11-12 — T029–T030

## Stack gate (config.yaml; ejecución nueva tras la última edición)
- format `--verify-no-changes` → PASS (exit 0)
- build → PASS (0 avisos, 0 errores)
- unit → PASS — 439/439
- integration → PASS — 11/11
- `git diff HEAD -- src/Infrastructure/Agent src/Application/Agent` vacío: el agente SK no se toca (AC11).

## Task done-checks
- [x] T029 — `MafAgentRunnerTests` y `MafChatClientFactoryTests` rojos (CS0234/CS0246: `Infrastructure.MafAgent.Runner` ausente). Con un `IChatClient` falso con guion (`ScriptedChatClient`), sin red:
  - el modelo recibe 20 herramientas (12 de lectura + 8 de propuesta), ninguna de borrado ni de escritura de catálogos (AC8);
  - las instrucciones contienen las 4 reglas de plan §0 (idioma, no soportado, `MissingOrAmbiguous`, `Europe/Madrid`);
  - con `FakeTimeProvider` en `2026-11-10T23:30:00Z` el modelo recibe `2026-11-10T23:30:00Z`, `2026-11-11T00:30:00+01:00` y `Europe/Madrid` (AC19);
  - la sesión serializada la restaura un runner NUEVO con sus turnos previos (AC15); `AppendSystemNoteAsync` añade la nota sin llamar al modelo y el turno siguiente la ve;
  - `ProposedActions` son las del turno y de la conversación, y un turno sin propuestas devuelve 0;
  - 6 llamadas a herramientas con límite 5 → `AgentLoopExhaustedException(5)`, la herramienta se ejecuta 5 veces y el modelo se llama 6; 5 llamadas → sin error; el contador es por turno (AC14);
  - `TimeoutRejectedException`, directa o envuelta → `AgentTimeoutException`; otra excepción no se disfraza (AC14);
  - el log de herramientas tiene nombre, conversación y resultado, y nunca argumentos, resultados ni el texto del mensaje (sink en memoria);
  - proveedor: Ollama → cliente OpenAI-compatible con su `Endpoint` y el modelo de razonamiento; Azure → `DeploymentName` y host de Azure; OpenAI sin endpoint → `ModelId`; Azure sin endpoint → error de configuración.
- [x] T030 — T029 verde (18/18 + DI 2/2); `UNIT` verde (439/439). `MafAgentRunnerDiTests` sobre el host de tests: runner, herramientas y fábrica resueltos desde el registro real, scoped (las propuestas no se comparten entre peticiones), y la fábrica real construye el cliente desde la sección `Ai` de prueba.

## Control de los gates propios (mutantes)
- 17 mutantes: 16 detectados (uno, "sin `Terminate`", tras fijar en el test que el modelo se llama exactamente 6 veces); 1 no compila (quitar el `= null` de un parámetro: el compilador lo impide porque le siguen parámetros opcionales). Ficheros restaurados byte a byte.

## Capas no ejecutadas
- **SUSTITUIDA** — modelo real: aquí un `IChatClient` con guion. La elección de herramientas con qwen2.5:3b es la checklist manual (T035). El timeout real del pipeline `InferenceClient` (Polly) no se ejecuta: se simula su excepción.

## Hallazgos descartados
- ninguno

## Verdict: PASS (alcance de tareas T029–T030). Listo para commit.
