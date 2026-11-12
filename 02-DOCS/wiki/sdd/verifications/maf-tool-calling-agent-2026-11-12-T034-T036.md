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
  - [ ] Checklist manual con Ollama: **ejecutada** el 2026-11-12 (`MafAgentModelChecklistTests`, Category=Manual): **9/9 FALLO** con `qwen2.5:3b-instruct`. El modelo inventa `portfolioId: 1` en vez de consultar `ListMyPortfolios` (casos 2–6 y 9), inventa cantidad/precio/fecha en vez de preguntar (8), omite el símbolo (6, 8), responde en inglés a una pregunta en español (1) y ofrece una confirmación de borrado por texto inexistente (7). Ningún caso escribió datos ni dejó una acción con datos inventados. Resultados por caso en `docs/maf-agent-flow.md`.
- [ ] T036 — gate verde (arriba), pero "cada fila marcada con su evidencia" no se cumple mientras T035 esté parcial.

## Acceptance criteria (spec)
Cubiertos por tests deterministas (registros por bloque en `02-DOCS/wiki/sdd/verifications/`): AC2–AC7, AC10, AC10b, AC11, AC12, AC13, AC14, AC15, AC16, AC17, AC18, AC19 (inyección de hora y reglas), AC1 (paridad de datos con el plugin SK) y AC1b (datos de las herramientas).
- **UNVERIFIED con modelo real:** la selección de herramientas por un modelo real para AC1, AC8 (explica "no soportado" sin proponer), AC9 (pide el dato que falta), la resolución de "ayer" en Madrid (AC19) y el idioma de la respuesta. Las piezas deterministas (las herramientas que existen y las que no, `MissingOrAmbiguous`, el contexto horario y las instrucciones) sí están probadas; lo que falta es ver al modelo usarlas, que es exactamente la checklist T035.

## Capas no ejecutadas
- ninguna: el modelo real se ejecutó (ver T035).

## Hallazgos descartados
- ninguno

## Verdict: FAIL — AC1 (selección y uso de herramientas), AC8 (sin ofrecer acciones inexistentes), AC9 (pedir el dato) y el idioma fallan con el modelo real `qwen2.5:3b-instruct`. El código, la suite, la integración contra Postgres y la regresión SK están verdes, y ninguna garantía de escritura se rompió. Siguiente: reforzar instrucciones/herramientas (implement) o decidir otro modelo (clarify), y repetir la checklist.
