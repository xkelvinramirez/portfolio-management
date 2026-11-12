---
type: verification
title: Verification — maf-tool-calling-agent — 2026-11-12 — T035–T036 (checklist con 7B y gate final)
description: Cierra T035 y T036. Checklist manual con qwen2.5:7b-instruct 9/9 OK y gate final verde. Sustituye el veredicto FAIL del registro T034–T036.
tags: [sdd, verification]
timestamp: 2026-11-12T21:30:00Z
topic: sdd
slug: maf-tool-calling-agent
source_state: a77e8ae on feature/maf-tool-calling-agent-pr5 (dirty — only docs/maf-agent-flow.md and 02-DOCS)
---

# Verification — maf-tool-calling-agent — 2026-11-12 — T035–T036

## Stack gate (ejecución nueva tras la última edición)
- format `--verify-no-changes` → PASS (exit 0)
- build → PASS (0 avisos, 0 errores)
- unit → PASS — 471/471
- integration → PASS — 12/12 (contra el Postgres de desarrollo)

## Task done-checks
- [x] T034 — sin cambios desde el registro T034–T036 (regresión SK 11/11; nada tocado en `*/Agent/` ni en SemanticKernel).
- [x] T035 — `docs/maf-agent-flow.md` completo. Checklist manual (`MafAgentModelChecklistTests`, Category=Manual), con la app real, el Postgres de desarrollo y Ollama, una conversación nueva por caso:
  - 1.ª ejecución, `qwen2.5:3b-instruct`: 9/9 FALLO.
  - 2.ª ejecución, tras el refuerzo de a77e8ae (portafolios con id en el contexto y reglas explícitas), mismo 3B: 6/9 OK. Fallan las tres escrituras.
  - **3.ª ejecución, `qwen2.5:7b-instruct`, mismo código: 9/9 OK.** Detalle por caso en el doc.
- [x] T036 — gate verde (arriba) y cada fila de la checklist tiene su evidencia en el doc.

## Acceptance criteria (spec)
- Deterministas: igual que en el registro T034–T036 (AC1–AC19 cubiertos por tests).
- Con modelo real (`qwen2.5:7b-instruct`), antes UNVERIFIED y ahora observados:
  - **AC1**: elige la herramienta correcta con el id real (casos 1–5 y 9).
  - **AC8**: explica que borrar no está soportado, sin proponer nada ni ofrecer confirmación (caso 7).
  - **AC9**: no inventa datos y pide el que falta (caso 8).
  - **AC19**: "ayer" se resuelve como `2026-11-11` (caso 6).
  - **Idioma**: responde en español (1–8) y en inglés (9).

## Capas no ejecutadas
- **SUSTITUIDA** — modelo real en CI: la checklist es manual y de una sola ejecución por modelo. No detecta la variación entre ejecuciones de un modelo no determinista ni regresiones futuras del prompt.

## Hallazgos descartados
- Redacción menor con 7B: añade "euros"; en el caso 8 dice "Propuse…" sin haber propuesto nada; pide los campos de uno en uno. Ninguno cambia datos ni deja acciones; quedan como mejora opcional (devolver todos los campos que faltan a la vez).

## Verdict: PASS — con `qwen2.5:7b-instruct` como modelo local mínimo. Con `qwen2.5:3b-instruct` solo es fiable la lectura.
