---
type: verification
title: Verification — maf-tool-calling-agent — 2026-11-12 — T025–T026 (pre-commit)
description: Verificación de alcance de tarea antes del commit de las herramientas de lectura del agente MAF. No es el veredicto de la feature.
tags: [sdd, verification]
timestamp: 2026-11-12T11:30:00Z
topic: sdd
slug: maf-tool-calling-agent
source_state: 70190f9 on feature/maf-tool-calling-agent-pr4 (dirty — the T025–T026 changes under verification)
---

# Verification — maf-tool-calling-agent — 2026-11-12 — T025–T026

## Stack gate (config.yaml; ejecución nueva tras la última edición)
- format `--verify-no-changes` → PASS (exit 0, tras `dotnet format --include` del fichero de tests)
- build → PASS (0 avisos, 0 errores)
- unit → PASS — 368/368
- integration → PASS — 11/11

## Task done-checks
- [x] T025 — `MafAgentReadToolsTests` rojo (CS0234/CS0246: `Infrastructure.MafAgent.Tools` y `MafAgentReadTools` ausentes). 32 tests. **Paridad (AC1):** las 5 herramientas heredadas se comparan con `PortfolioAgentPlugin` construido sobre los MISMOS dobles de repositorio y el seed multi-exchange de `PortfolioAgentPluginTests` (más un punto de ETH): `ListMyPortfolios`, `GetPortfolioValue`, `GetPortfolioHoldings` (orden incluido), `GetAssetPeakOrTroughDate` (5 casos: max/min, mayúsculas, rango, "highest", " MIN "), `GetPortfolioAssetValueComparison` (BTC en dos exchanges, SOL presente en una sola fecha) y fechas inválidas o invertidas (el plugin SK lanza `ArgumentException`; la herramienta devuelve `InvalidDates`). Portafolio de otro usuario → `NotFound` en las 4 herramientas con id y sin leer sus entradas. **Resto (AC1b):** entries (todas las páginas), history (rango opcional, `InvalidDates`, `NotFound`), allocation por activo, catálogos (el exchange nunca expone `ApiKey`), notas sin contenido en el listado y con contenido en `GetNote`, nota inexistente → `NotFound`. `GetTools()` expone exactamente los 12 nombres del plan, sin sufijo `Async`, con descripción y sin parámetro `userId`; invocar el `AIFunction` ejecuta la herramienta.
- [x] T026 — T025 verde (32/32).

## Control de los gates propios (mutantes)
- 14 mutantes, todos detectados: sin dueño (4) · sin orden de holdings (1) · `min` sensible a mayúsculas (0 → 1 tras añadir `" MIN "`) · símbolo sensible (1) · sin fechas invertidas (1) · sin activo de una sola fecha (1) · solo primera página (1) · exchange con `ApiKey` (1) · historial sin tope (0 → 1 tras añadir un rango que excluye el último punto) · agrupación por exchange (1) · contenido en el listado de notas (1) · herramienta ausente (1) · holdings a otra fecha (1) · pico sin tope (1). Fichero restaurado byte a byte.

## Capas no ejecutadas
- **SUSTITUIDA** — consultas reales por MediatR y repositorios reales: aquí dobles. Las consultas existentes ya tienen sus tests; la selección de herramientas con modelo real es la checklist manual (T035).

## Hallazgos descartados
- ninguno

## Verdict: PASS (alcance de tareas T025–T026). Listo para commit.
