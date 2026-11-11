---
type: progress
title: Progress — Agente MAF con tool-calling
description: Ledger append-only de implement para maf-tool-calling-agent. Fuente de verdad para reanudar tras compactación o reinicio.
tags: [sdd, progress]
timestamp: 2026-11-11T23:00:00Z
topic: sdd
slug: maf-tool-calling-agent
---

# Progress — Agente MAF con tool-calling

> Plan: [../plans/maf-tool-calling-agent.md](../plans/maf-tool-calling-agent.md) · Config: [../config.yaml](../config.yaml)
> Append-only. Una tarea con `status: complete` NO se vuelve a ejecutar.

## Pre-implement — 2026-11-11
- status: complete
- sdd-init: config.yaml escrito; registro de skills refrescado (273).
- infra: `dotnet test` estaba roto desde 7190d30 (xunit.v3 sin opt-in de MTP); `global.json` añadido (commit 1ff94de). Baseline unit: 145/145.
- skill resolution: used [implement, sdd-init]; .NET mechanics from dotnet-claude-kit (testing); missing []; fallback [none].

## T039 — 2026-11-11
- status: complete
- kind: characterization (baseline del agente SK antes de MAF; tests verdes por diseño)
- red: mutación deliberada de 2 expectativas (nombre `GetPortfolioHoldings`→`…MUTANT`, valor 230→231) → 2 fallos por aserción ("Expected 231M, but found 230M"; nombre distinto en índice 2); restaurado.
- green: `dotnet test --project tests/WebApi.MinimalAPI.Tests --filter-class "*SkAgentRegressionTests"` → 11/11.
- refactor: `TestContext.Current.CancellationToken` en las llamadas (xUnit1051, 8 avisos → 0).
- suite: unit 156/156 (145 + 11).
- files: tests/WebApi.MinimalAPI.Tests/Agent/SkAgentRegressionTests.cs
- cubre: kernel con plugin `PortfolioOperations` (4 funciones: ListMyPortfolios, GetPortfolioValue, GetPortfolioHoldings, GetAssetPeakOrTroughDate), filtros LoopCapping/ToolInvocationAudit, servicios de chat Fast/Reasoning sin red; datos de las 4 funciones (scoping por usuario, valor 230, holdings ordenados, pico/valle con y sin rango, símbolo desconocido → null).
- note: el predicado de ListMyPortfolios se verifica compilándolo (UserId 7 sí, 8 no).
- no toca código bajo `*/Agent/`.
- decision: none
- blocker: none
