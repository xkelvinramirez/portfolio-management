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

## T039 — revisión por tarea — 2026-11-11
- status: complete
- review: changes-needed (0 critical, 1 important, 3 minor) → Important corregido.
- important (corregido): el test de holdings "ordenados desc." no podía fallar (el seed ya venía ordenado y GroupBy conserva el orden). Seed invertido (ETH antes que BTC). Prueba: quitar temporalmente `OrderByDescending` del plugin → 1 fallo ("Expected holdings to be equal to…"); plugin restaurado con `git checkout` (sin cambios en src/).
- minor (diferidos a review final): (1) Id del resumen de ListMyPortfolios queda en 0 (Id con `private init`); (2) los tests fijan KernelFactory con un ServiceCollection propio, no las registraciones de producción ni el valor de `MaxAllowedToolLoops`; (3) `HttpClient` del doble no se libera ("sin red" por construcción).
- suite: unit 156/156.

## T040 — 2026-11-11
- status: complete
- red: `dotnet test --project tests/Application.Tests --filter-class "*LoggingBehaviorRedactionTests"` → 1 fallo por aserción (los 10 centinelas, incl. `PendingActions[]` anidado, aparecían en el log); 2 guardas verdes (no sensibles sí se registran; `Password` ya oculto). Commit 6dc1934 (rojo intencionado).
- files: tests/Application.Tests/Common/Behaviors/LoggingBehaviorRedactionTests.cs
- decision: none · blocker: none

## T041 — 2026-11-11
- status: complete
- green: `SensitivePropertyNames` += Message, Reply, Values, Before, Result, Content, Summary → T040 3/3; unit 159/159; format y build limpios.
- verify (alcance tarea): PASS — ver verifications/maf-tool-calling-agent-2026-11-11-T041.md.
- per-task review: omitida (cambio trivial de una lista de nombres; test visto rojo y verde).
- finding (para tasks/config): el comando `integration` de config.yaml devuelve exit 8 mientras no haya tests de integración (MTP: cero tests) → añadir `--minimum-expected-tests 0` antes de T011.
- files: src/Application/Common/Behaviors/LoggingBehavior.cs
- decision: none (ya registrada en decisions.md, post-analyze 3) · blocker: none

## config — 2026-11-11
- status: complete
- fix: comando integration con `--ignore-exit-code 8` (MTP: 8 = cero tests). `--minimum-expected-tests 0` probado y descartado (sigue en exit 8). Verificado: exit 0 con 0 tests. Límite: el control negativo (un test de integración que falle → exit 2) se comprobará en T011.
