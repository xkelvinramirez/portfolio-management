---
type: verification
title: Verification — maf-tool-calling-agent — 2026-11-12 — T027–T028 (pre-commit)
description: Verificación de alcance de tarea antes del commit de las herramientas de propuesta del agente MAF. No es el veredicto de la feature.
tags: [sdd, verification]
timestamp: 2026-11-12T13:00:00Z
topic: sdd
slug: maf-tool-calling-agent
source_state: b755f3a on feature/maf-tool-calling-agent-pr4 (dirty — the T027–T028 changes under verification)
---

# Verification — maf-tool-calling-agent — 2026-11-12 — T027–T028

## Stack gate (config.yaml; ejecución nueva tras la última edición)
- format `--verify-no-changes` → PASS (exit 0)
- build → PASS (0 avisos, 0 errores)
- unit → PASS — 411/411
- integration → PASS — 11/11

## Task done-checks
- [x] T027 — `MafAgentProposeToolsTests` rojo (CS0246: `MafAgentProposeTools` ausente). 43 tests:
  - datos completos → 1 `PendingAction` preparada con `Add`, de la conversación del turno, y solo se envían consultas (0 comandos);
  - dato que falta o inválido → 0 acciones y `MissingOrAmbiguous:<campo>` (portfolioId, symbol, exchangeName, quantity ≤ 0, pricePerUnit < 0, recordedAt ausente o "ayer", name, category, title, content);
  - símbolo o exchange desconocido → `UnknownCatalogItem:<valor>`; símbolo duplicado → `MissingOrAmbiguous:symbol`; exchange recortado y sin distinguir mayúsculas;
  - portafolio de otro usuario → `NotFound` (crear entrada, refresco);
  - 3 propuestas en un turno → 3 acciones independientes; `BeginTurn` reinicia la lista; proponer sin `BeginTurn` → `InvalidOperationException`;
  - ningún parámetro `userId` (esquema y reflexión);
  - `ProposeUpdate*` fusiona solo lo pedido y guarda la petición completa: cambiar solo `quantity` conserva el resto y los ids, y `Before` es el estado actual; cambiar solo el nombre conserva la descripción y viceversa; editar el título de una nota inactiva conserva `IsActive=false`; sin cambios reales → `MissingOrAmbiguous:changes`; símbolo o exchange nuevo resuelto, o desconocido → `UnknownCatalogItem`;
  - `ProposeSendStatusReport` sin periodo → el mes natural anterior calculado al proponer; medio periodo o invertido → `MissingOrAmbiguous:period`;
  - fechas con `Z`, con `+01:00` y sin offset (Madrid, también en horario de verano) → el mismo instante con `Kind=Utc`, también invocando el `AIFunction`;
  - el `Summary` no contiene el título ni el contenido de la nota.
- [x] T028 — T027 verde (43/43); lectura sigue verde (32/32).

## Control de los gates propios (mutantes)
- 19 mutantes, todos detectados (2 tras añadir tests: descripción conservada al cambiar solo el nombre, y símbolo desconocido al editar una entrada). Fichero restaurado byte a byte.

## Capas no ejecutadas
- **SUSTITUIDA** — consultas reales, lector real y repositorio real: aquí dobles. El flujo propuesta → confirmación por HTTP lo prueban T031/T033; la elección de herramientas con modelo real es la checklist manual (T035).

## Hallazgos descartados
- ninguno

## Verdict: PASS (alcance de tareas T027–T028). Listo para commit.
