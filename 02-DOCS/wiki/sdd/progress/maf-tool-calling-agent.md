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

## T001 — 2026-11-11
- status: complete
- decision (usuario): MAF 1.28.0; transitivo MEAI 10.5→10.11 y OpenAI 2.10→2.14 aceptado; SK 1.81.0 sin cambios.
- red: el spike es exploratorio. Las pruebas en vivo dependen del modelo; la observación del límite de iteraciones se fijó tras medirla. Primer intento de compilación con 4 errores (ambigüedad `FunctionCallContent` MEAI/SK; sobrecarga `ChatMessage`), corregidos.
- green: offline 2/2 (Azure sin red; límite de iteraciones no lanza); manual contra Ollama 3/3 dos veces (herramienta tipada; sesión restaurada en un agente nuevo; llamada real a función de SK bajo MEAI 10.11).
- suite: unit 161/161; T039 11/11; sin vulnerabilidades.
- files: src/Infrastructure/Infrastructure.csproj, tests/WebApi.MinimalAPI.Tests/MafAgent/Spike/MafSpikeTests.cs, docs/maf-agent-packages.md
- verify: PASS (verifications/maf-tool-calling-agent-2026-11-11-T001.md)
- model note: qwen2.5:3b-instruct (CPU, tools); 19 herramientas pendiente de T035.
- per-task review: pendiente (ver checkpoint).
- blocker: none

## T001 — revisión por tarea — 2026-11-11
- status: complete
- review: changes-needed (0 critical, 1 important, 3 minor).
- important (corregido): la observación del límite de iteraciones pasaba aunque la herramienta no se ejecutara nunca (`ValueCalls <= 3`). Ahora exige `== 3` y usa `NotThrowAsync`. Prueba: con la expectativa a 2 falla con "but found 3".
- minor (corregido): la comprobación del JSON de sesión exige ahora el resultado de la herramienta (`1234.5`), no una palabra del prompt. Manual 3/3.
- minor (corregido): `docs/maf-agent-packages.md` resume también los saltos transitivos no IA (System.ClientModel, VectorData.Abstractions, nuevos AI.Evaluation/ML.Tokenizers/Google.Protobuf).
- minor (diferido a T029/T030): el cliente Azure se construye con valores fijos y no desde `AiOptions`/configuración. T029 ya exige probar la selección de proveedor desde `AiOptions`. Además, `agent.Should().NotBeNull()` es trivial; el "sin red" se prueba por construcción.

## Rebase sobre development (PR #19) — 2026-11-11
- status: complete
- contexto: `origin/development` recibió el PR #19 (`feature/portfolio-asset-value-comparison`), que añade al agente SK la 5.ª función `GetPortfolioAssetValueComparison` y `PortfolioAgentPluginTests`. El worktree se había cambiado a `development` y actualizado desde el IDE (reflog: checkout + pull); se volvió a `feature/maf-tool-calling-agent` sin pérdida.
- rebase: 11 commits locales sin publicar, sin conflictos.
- red (esperado): T039 `CreateKernel_…` falló: "found one extraneous item at index 4: GetPortfolioAssetValueComparison". La red de seguridad detectó el cambio de base.
- green: T039 actualizado a la nueva línea base (5 funciones; los datos de la comparación ya los fija `PortfolioAgentPluginTests`). Unit 165/165, integration exit 0, spike manual 3/3, format limpio.
- **trigger de spec:** la decisión diferida "comparación entre fechas… hasta que esa feature llegue a development" ya se cumple. Pendiente de decidir (clarify) si AC1 vuelve a 5 preguntas y el agente MAF añade la herramienta de comparación. No afecta al PR1.
