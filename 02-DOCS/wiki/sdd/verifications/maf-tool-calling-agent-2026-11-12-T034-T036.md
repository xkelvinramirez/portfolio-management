---
type: verification
title: Verification — maf-tool-calling-agent — 2026-11-12 — T034–T036 (regresión, docs y gate final)
description: Regresión del agente SK y de la suite, documentación del flujo y gate final de la feature. T035 queda parcial: la checklist manual con Ollama no se ha ejecutado.
tags: [sdd, verification]
timestamp: 2026-11-12T19:30:00Z
topic: sdd
slug: maf-tool-calling-agent
source_state: a527787 on feature/maf-tool-calling-agent-pr5 (dirty — only docs/maf-agent-flow.md and 02-DOCS under verification)
---

# Verification — maf-tool-calling-agent — 2026-11-12 — T034–T036

## Stack gate (ejecución nueva tras la última edición)
- format `--verify-no-changes` → PASS (exit 0)
- build → PASS (0 avisos, 0 errores)
- unit → PASS — 469/469
- integration → PASS — 12/12 (contra el Postgres de desarrollo)

## Task done-checks
- [x] T034 — regresión (AC11):
  - `git diff --stat development -- src/Infrastructure/Agent src/Application/Agent src/WebApi.MinimalAPI/Endpoints/Agent` → vacío.
  - `git diff development -- src/Infrastructure/Infrastructure.csproj` filtrado a líneas `+`/`-` con `SemanticKernel` → vacío.
  - `SkAgentRegressionTests` (T039) → 11/11.
  - Versiones resueltas (`project.assets.json` de Infrastructure) = las aprobadas en `docs/maf-agent-packages.md`: `Microsoft.Agents.AI` 1.28.0, `Microsoft.Extensions.AI`/`.Abstractions`/`.OpenAI`/`.Evaluation` 10.11.0, `Microsoft.SemanticKernel` 1.81.0, `OpenAI` 2.14.0, `System.ClientModel` 1.15.0, `Microsoft.Extensions.VectorData.Abstractions` 10.10.0, `Microsoft.ML.Tokenizers` 2.0.0, `Google.Protobuf` 3.30.2.
- [ ] T035 — **parcial**:
  - [x] `docs/maf-agent-flow.md` creado: endpoints, flujos A (proponer), B (confirmar) y C (rechazar), errores, límites, fechas, seguridad, configuración y decisiones.
  - [ ] Checklist manual con Ollama (las 5 preguntas de AC1, registrar con "ayer", borrar, compra sin precio, pregunta en inglés): **NO ejecutada**. Ollama no respondía en `localhost:11434` (timeout). La tabla del doc queda con los 9 casos en PENDIENTE.
- [ ] T036 — gate verde (arriba), pero "cada fila marcada con su evidencia" no se cumple mientras T035 esté parcial.

## Acceptance criteria (spec)
Cubiertos por tests deterministas (registros por bloque en `02-DOCS/wiki/sdd/verifications/`): AC2–AC7, AC10, AC10b, AC11, AC12, AC13, AC14, AC15, AC16, AC17, AC18, AC19 (inyección de hora y reglas), AC1 (paridad de datos con el plugin SK) y AC1b (datos de las herramientas).
- **UNVERIFIED con modelo real:** la selección de herramientas por un modelo real para AC1, AC8 (explica "no soportado" sin proponer), AC9 (pide el dato que falta), la resolución de "ayer" en Madrid (AC19) y el idioma de la respuesta. Las piezas deterministas (las herramientas que existen y las que no, `MissingOrAmbiguous`, el contexto horario y las instrucciones) sí están probadas; lo que falta es ver al modelo usarlas, que es exactamente la checklist T035.

## Capas no ejecutadas
- **HERRAMIENTA-AUSENTE** — modelo real (Ollama): no disponible en esta sesión; no se ejecutó nada en su lugar.

## Hallazgos descartados
- ninguno

## Verdict: FAIL (solo por T035) — el código, la suite, la integración contra Postgres y la regresión del agente SK están verdes. Falta ejecutar la checklist manual con Ollama y anotar OK/FALLO en `docs/maf-agent-flow.md`; después, T036 pasa.
