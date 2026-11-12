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

## T003 + T004 — 2026-11-11
- status: complete
- red: `MafConversationTests` no compilaba (CS0246, tipo ausente).
- green: 6/6. Mutación `>`→`>=` en IsExpired → falla el test de 24 h exactas.
- contract (M6, aceptado; fijado aquí): `MafConversation.Create(long userId, string encryptedSessionState, DateTime nowUtc)`, `IsExpired(nowUtc)` (> 24 h), `Touch(nowUtc)`, `UpdateSession(string encrypted, DateTime nowUtc)`. Id = `Guid.CreateVersion7(nowUtc)`.
- files: src/Domain/Entities/MafConversation.cs, tests/Domain.Tests/Entities/MafConversationTests.cs

## T005 + T006 — 2026-11-11
- status: complete
- red: `PendingActionTests` no compilaba (CS0246).
- green: 15/15. Mutación `>=`→`>` en IsExpired → falla el test de 10 min exactos.
- decision: las transiciones inválidas lanzan `InvalidOperationException` (Domain no tiene ErrorOr; los handlers comprueban el estado antes, así que es un error de programación, no de negocio). MarkExecuting/MarkRejected solo desde Pending; MarkSucceeded/MarkFailed/MarkStale solo desde Executing.
- files: src/Domain/Entities/PendingAction*.cs, tests/Domain.Tests/Entities/PendingActionTests.cs

## T007 — 2026-11-11
- status: complete
- decision: `Values`/`Before` como `IReadOnlyDictionary<string, string?>` (mismo formato que el snapshot de ICurrentValuesReader); `Result` como `object?`.
- files: src/Contracts/MafAgent/*.cs

## T008 — 2026-11-11
- status: complete
- check: refs prohibidas en Application con controles positivo y negativo; puertos públicos; InternalsVisibleTo.
- files: src/Application/MafAgent/Interfaces/*.cs, src/Application/Application.csproj
- verify: PASS (verifications/maf-tool-calling-agent-2026-11-11-T003-T008.md); unit 186/186.
- per-task review: pendiente (agrupada T003–T008).

## T003–T008 — revisión por bloque — 2026-11-11
- status: complete
- review: VERDICT pass (0 critical, 0 important, 4 minor).
- minor (diferidos a la revisión final): (1) `UpdateSession` va más allá del plan, pero está testeado y registrado como contrato M6; (2) `InternalsVisibleTo` de Infrastructure → lo cubre T009; (3) `Guid.CreateVersion7(nowUtc)` reinterpreta fechas con Kind distinto de Utc (§0 ya lo prohíbe; documentar o asertar); (4) `MarkFailed` no protege contra un errorCode vacío.

## Ramas — 2026-11-11
- PR2 movido a `feature/maf-tool-calling-agent-pr2` (apilada sobre el PR1). Un push ajeno (18:44, probablemente el IDE) había publicado T003–T008 en `origin/feature/maf-tool-calling-agent`. El force-push para devolver el PR1 a `ec54b8a` lo bloquea el hook `pre-bash-guard` de dotnet-claude-kit: **pendiente de que el usuario lo ejecute**. `feature/maf-tool-calling-agent-pr2` publicada.

## T009 — 2026-11-11
- status: complete
- files: src/Infrastructure/MafAgent/Persistence/{MafConversationConfiguration,PendingActionConfiguration,MafConversationRepository,PendingActionRepository}.cs, AppDbContext (DbSets), DependencyInjection (repos), Infrastructure.csproj (InternalsVisibleTo)
- decision: enums como texto (auditoría legible; un cambio de orden del enum no reasigna filas). Repos directos sobre AppDbContext, porque `Repository<T>` exige `Entity` con id long.

## T010 — 2026-11-11
- status: complete
- migration: 20261111175145_AddMafAgent (2 tablas + 3 índices, solo aditiva). Aplicada al Supabase de desarrollo (confirmado por el usuario).

## T011 — 2026-11-11
- status: complete
- red: 9/9 con `NotImplementedException: T012` (con la siembra contra la base funcionando).
- harness (M8, aceptado; fijado aquí): cadena desde la variable de entorno `ConnectionStrings__Database` o los user-secrets de WebApi (`daa01fcc-…`); un usuario temporal por test, borrado en `DisposeAsync`; concurrencia con dos `AppDbContext` independientes.

## T012 — 2026-11-11
- status: complete
- green: 9/9 integración; `ExecuteUpdate` = 3; `TrackedChangesDiscarder` (`ChangeTracker.Clear()`) + tests 2/2 (EF InMemory añadido al proyecto de tests).
- mutantes: sin `Status==Pending` → 4 fallos (incl. concurrencia); sin `ExpiresAt>now` → 2 fallos.
- suite: unit 188/188, integración 9/9.
- verify: PASS (verifications/maf-tool-calling-agent-2026-11-11-T009-T012.md)

## T009–T012 — revisión por bloque — 2026-11-11
- status: complete
- review: VERDICT pass (0 critical, 0 important, 5 minor).
- minor (corregido): falta el borde exacto de caducidad en el claim → `TryClaim_AtTheExactTenMinuteMark_ReturnsFalse`; el mutante `ExpiresAt > now` → `>=` ahora falla. Integración 10/10.
- minor (corregido): barrido de usuarios `maf-it-*@test.local` residuales en `InitializeAsync` (por si se mata el proceso a mitad de test).
- minor (diferidos): (1) sin cadena de conexión, los tests INTEG fallan en vez de saltarse; se ejecutan con el filtro `Category=Integration` de config.yaml y el `UNIT` los excluye; (2) "no Pending" en Reject solo prueba Executing (el mutante de estado ya muere); (3) `ExecuteUpdate` deja obsoleta una entidad ya cargada en el tracker: por diseño, T019/T020 deben mantener el orden MarkExecuting/DiscardAll.

## Ramas — force-push del PR1 — 2026-11-11
- El usuario ejecutó el force-push: `origin/feature/maf-tool-calling-agent` = `ec54b8a` (solo el PR1). La rama local del PR1 está abierta en la carpeta principal y sigue en `c3e4c05`; hay que alinearla allí (`git reset --keep origin/feature/maf-tool-calling-agent`) para que un "Sync" del IDE no vuelva a subir T003–T008.

## T037 + T038 — 2026-11-11
- status: complete
- red: tests no compilaban (CS0246, `AddedEntityIdLog` ausente).
- green: 5/5 InMemory + 1/1 Postgres; unit 193/193; integración 11/11.
- **hallazgo de test:** el mutante "registrar ids antes del commit" sobrevivía con InMemory (asigna ids en `Add`). Se añadió `AddedEntityIdLogIntegrationTests` (Postgres: `Id == 0` antes de guardar), que lo mata ("Expected 5L, but found 0L"). Desviación del done-check de T037: se añade una capa de integración.
- refactor: helper `DevDatabase` (cadena, usuario de test, barrido) y colección xUnit `DevDatabase` sin paralelismo, para que el barrido de una clase no borre usuarios vivos de otra.
- files: src/Infrastructure/MafAgent/Persistence/AddedEntityIdLog.cs, src/Infrastructure/DependencyInjection.cs, tests/WebApi.MinimalAPI.Tests/MafAgent/Persistence/{AddedEntityIdLogTests,AddedEntityIdLogIntegrationTests,DevDatabase}.cs, PendingActionRepositoryClaimTests.cs (usa el helper)
- verify: PASS (verifications/maf-tool-calling-agent-2026-11-11-T037-T038.md)

## T037–T038 — revisión por bloque — 2026-11-11
- status: complete
- review: VERDICT pass (0 critical, 0 important, 5 minor). DI y ciclo de vida correctos: la misma instancia scoped en el contexto y en `IAddedEntityIdLog`, sin dependencia cautiva.
- minor (diferidos): (1) reintentos de `EnableRetryOnFailure`: según el diseño de EF el reintento ocurre dentro de la estrategia, tras `SavingChanges`, y `SaveChangesFailed` solo salta al fallo final, así que la lista pendiente sobrevive. NO verificado en el código fuente de EF; riesgo bajo. (2) cableado DI no probado aquí: T033 (ServiceProvider real) debe afirmar id > 0 e igual al de la fila. (3) solo la ruta async contra Postgres. (4) otro proceso de tests concurrente sobre la base compartida (aceptado). (5) línea en blanco extra en PendingActionRepositoryClaimTests.
- PR2 pendiente: solo T002 (infraestructura de tests HTTP).

## T002 — 2026-11-11
- status: complete
- red/green: smoke 4/4 a la primera; verificado con mutantes (repositorio real → 2 fallos; sin doble de Cosmos → 4 fallos).
- harness: `MafAgentApiFactory` (Testing, settings en memoria, EF InMemory + AddedEntityIdLog, doble de transiciones, dobles de Cosmos/runner/email/CoinGecko, FakeTimeProvider, auth por cabecera `X-Test-UserId`). Retira solo el hosted service de HealthChecks UI.
- decision: `InternalsVisibleTo(WebApi.MinimalAPI.Tests)` en WebApi.csproj en lugar de `public partial class Program` (sin tocar código).
- finding (fuera de alcance): HealthChecks.UI no carga IdentityModel 5.2 en la salida de tests; revisar en la API desplegada.
- suite: unit 197/197, integration 11/11.
- verify: PASS (verifications/maf-tool-calling-agent-2026-11-11-T002.md)
- **PR2 completo:** T002–T012, T037, T038.

## T002 — revisión por tarea — 2026-11-11
- status: complete
- review: VERDICT pass (0 critical, 0 important, 4 minor). El `InternalsVisibleTo` de WebApi no contradice "sin tocar código", pero es una excepción a M1 a nivel de proyecto (registrada aquí).
- **pautas obligatorias para T031** (minor 2): `Runner`, `Time` y la base InMemory se comparten dentro de un `IClassFixture`. Cada test usa ids de usuario propios y `Runner.ClearReceivedCalls()` (o una factoría por test) para que las aserciones de AC4 (`DidNotReceive`) y los `Returns` no se contaminen; no avanzar `Time` sin restaurarlo.
- minor (diferidos): (1) no está verificado que `UseSetting` gane a las variables de entorno (ApplicationInsights/Serilog); fijarlo con `ConfigureAppConfiguration` o un guard si aparece; (3) el doble de transiciones solo prueba claim y mark-failed (reject y borde de caducidad cubiertos contra Postgres en T011); InMemory ignora FK y unique; (4) la aserción `Contains("Proxy")` depende del nombre de Castle.

## Ramas — PR3 — 2026-11-11
- Rama `feature/maf-tool-calling-agent-pr3` creada desde `origin/feature/maf-tool-calling-agent-pr2` (f0f07c5), sin upstream, para que un push automático del IDE no acabe en el PR2.

## T013–T016 — 2026-11-11
- status: complete
- red: T013 no compilaba (CS0234/CS0246: `Payloads`, `PendingActionFactory`); T015 igual (`CurrentValuesReader`).
- green: factoría 37/37; desfase y lector 19/19; unit 253/253; integración 11/11.
- mutantes: 6, todos detectados (ver el registro de verificación).
- **decisión (hueco del plan):** payloads tipados propios en `Application/MafAgent/Payloads` (id objetivo con `[PayloadTarget]` y todos los campos de la petición completa), porque `Update*Request` no lleva el id que necesita el comando. `Before` es el `FieldSnapshot` del lector. `BusinessTime` centraliza `Europe/Madrid` y los dos formatos de presentación.
- files: src/Application/MafAgent/{PendingActionFactory,FieldSnapshot,BusinessTime,StaleCheck,CurrentValuesReader}.cs, src/Application/MafAgent/Payloads/PendingActionPayloads.cs, tests/Application.Tests/MafAgent/{PendingActionFactoryTests,StaleCheckTests,CurrentValuesReaderTests}.cs
- verify: PASS (verifications/maf-tool-calling-agent-2026-11-11-T013-T016.md)

## T013–T016 — revisión por bloque — 2026-11-11
- status: complete
- review: VERDICT pass (0 critical, 2 important, 5 minor).
- important #1 descartado con evidencia: "G29" ya normaliza la escala de los decimales (ver la verificación de T017–T018).
- important #2 corregido: `FieldSnapshotTests` (Kind Utc/Unspecified/Local/DateTimeOffset).
- minor corregidos: #3 (comentario en StaleCheck), #4 (test del título con forma de fecha).
- **pauta para T019/T023 (minor 5):** `ReadPayload`/`ReadBefore`/`ToDto` lanzan con datos corruptos o que no se descifran. En confirm, la lectura tras el reclamo queda dentro del `catch` documentado (→ Failed). Antes del reclamo y en chat, una excepción ahí es un fallo de servidor (500), no de negocio.
- minor diferidos: #6 `BusinessTime.Zone` sin tzdata (igual que los handlers actuales); #7 visibilidad interna de helpers (sin impacto).

## T017–T018 — 2026-11-11
- status: complete
- red: CS0246 (`PendingActionExecutor` ausente). green: 11/11; unit 272/272; integración 11/11.
- mutantes: 5, todos detectados.
- decision: el ejecutor no reenvía su `ct` y despacha siempre con `CancellationToken.None` (más fuerte que "quien lo llama pasa None"). `StatusReportSentResult` es el resultado de un informe enviado.
- files: src/Application/MafAgent/PendingActionExecutor.cs, StaleCheck.cs (comentario), tests/Application.Tests/MafAgent/{PendingActionExecutorTests,FieldSnapshotTests,PendingActionFactoryTests}.cs
- verify: PASS (verifications/maf-tool-calling-agent-2026-11-11-T017-T018.md)

## T019–T020 — 2026-11-11
- status: complete
- red: CS0234/CS0246 (handler ausente). green: 19/19; unit 291/291; integración 11/11.
- mutantes: 8; 7 detectados y 1 equivalente (comprobación doble de dueño).
- decisiones: `MafAgentErrors` con los códigos exactos de plan §0. Orden previo al reclamo: dueño → caducidad (acción o conversación) → estado. Tras el reclamo: leer el payload, comprobar el desfase y ejecutar, todo dentro del único `catch (Exception)` documentado; así un payload corrupto acaba en `Failed` (pauta de la revisión de T013–T016). La nota de sesión se añade FUERA del `catch`: si fallara, sería un 500 y la acción quedaría en `Executing` (nunca se re-ejecuta, "como mucho una vez").
- files: src/Application/MafAgent/{MafAgentErrors.cs,Command/ConfirmPendingActionCommandHandler.cs,Command/ConfirmPendingActionCommandValidator.cs}, tests/Application.Tests/MafAgent/{ConfirmPendingActionCommandHandlerTests,ListLogger}.cs
- verify: PASS (verifications/maf-tool-calling-agent-2026-11-11-T019-T020.md)

## T017–T020 — revisión por bloque — 2026-11-11
- status: complete
- review: VERDICT fail (1 important, 2 minor, 5 mutantes supervivientes). Ejecutor y respuestas a las preguntas de runtime: correctos (el SaveChanges interno escribe `Executing` sobre una fila ya en `Executing`, inocuo; tras `DiscardAll` la UoW no guarda nada).
- important #1 corregido: la nota de sesión dentro del mismo `try`; si falla con el desenlace decidido, se devuelve y persiste el desenlace real sin nota. Sigue habiendo una única captura de `Exception` (dos ramas). Mi decisión anterior (nota fuera de la captura, 500 aceptado) era incorrecta: el plan solo acepta `Executing` permanente ante una caída del proceso.
- minor #2 corregido: aviso si `TryMarkFailedAsync` devuelve false. minor #3 → pauta para T031 (el endpoint confirm sin token).
- mutantes supervivientes: los 5 cubiertos con tests nuevos (unit 296/296).
- verify: PASS (verifications/maf-tool-calling-agent-2026-11-11-T020-review.md)

## T021–T022 — 2026-11-12
- status: complete
- red: CS0246 (comando, handler y validador ausentes). green: 15/15; unit 311/311; integración 11/11.
- mutantes: 8, todos detectados (el de dueño de la acción, tras añadir un test de acción ajena en conversación propia).
- decisiones: `RejectPendingActionCommand : IRequest<ErrorOr<Success>>` (el endpoint devolverá 204). **No es `ICommand`**: `TryRejectAsync` hace su propio commit y no queda nada para la UoW; la entidad cargada nunca se modifica, así la UoW no puede pisar un confirm concurrente. Mismo orden de comprobaciones que confirmar. Rechazar **no añade nota a la sesión**: ni el spec ni el plan la piden, y si el usuario pregunta, el agente puede consultar el estado. Se puede revisar en T023/T028 si hace falta.
- files: src/Application/MafAgent/Command/{RejectPendingActionCommandHandler,RejectPendingActionCommandValidator}.cs, tests/Application.Tests/MafAgent/RejectPendingActionCommandHandlerTests.cs
- verify: PASS (verifications/maf-tool-calling-agent-2026-11-12-T021-T022.md)
## T021–T022 — revisión por bloque — 2026-11-12
- status: complete
- review: VERDICT pass, sin hallazgos importantes. Comprobado sin defecto: paridad del WHERE de `TryRejectAsync` con el handler (`now >= ExpiresAt` frente a `ExpiresAt > now`), nada pasa por la UoW ni por la caché, y el validador se registra.
- minor #1 corregido: el mutante que quita la comprobación de dueño de la conversación sobrevivía; test nuevo (acción propia en conversación ajena) lo detecta. unit 312/312.
- minor #2 aceptado: la caducidad de la conversación por inactividad no está en el SQL atómico; un rechazo que cruza el límite de 24 h a la vez sigue siendo inocuo (no escribe datos de negocio). Confirmar tiene la misma forma.
## T023–T024 — 2026-11-12
- status: complete
- red: CS0246 (handler ausente). green: 24/24 (20 handler + 4 DI); unit 336/336; integración 11/11.
- mutantes: 13, todos detectados.
- decisiones: conversación nueva → el runner recibe sesión vacía `""` (el runner de T030 debe tratarla como sesión nueva) y la conversación se prepara con `Add` solo tras un turno correcto. Sesión corrupta → excepción (500), nada se guarda. El handler no tiene ejecutor ni `ISender`: no puede escribir. El chat sí usa el token de la petición (no escribe nada hasta el commit). `Application.Tests` añade `Microsoft.Extensions.DependencyInjection` 10.0.14 (la versión de `Microsoft.Extensions.Hosting` en Infrastructure) para el test de DI; MediatR exige `ILoggerFactory` registrado antes de `AddApplication`, como hace el host.
- files: src/Application/{DependencyInjection.cs,MafAgent/Command/SendMafAgentMessageCommandHandler.cs,MafAgent/Command/SendMafAgentMessageCommandValidator.cs}, tests/Application.Tests/{Application.Tests.csproj,MafAgent/SendMafAgentMessageCommandHandlerTests.cs,MafAgent/MafAgentApplicationDiTests.cs}
- verify: PASS (verifications/maf-tool-calling-agent-2026-11-12-T023-T024.md)
## T023–T024 — revisión por bloque — 2026-11-12
- status: complete
- review: VERDICT pass, sin hallazgos importantes. Descartado con evidencia: un turno fallido no guarda nada (`UnitOfWorkBehavior` solo guarda sin error; el `DbContext` es por petición); EF ordena las inserciones por FK aunque la conversación se añada después de las acciones; `Encrypt("")` ida y vuelta correcto; el host registra logging antes de `AddApplication`.
- minor corregido: el mutante "texto del mensaje en el log de timeout" sobrevivía (el de límite de pasos ya lo detectaba); el test de timeout ahora lo comprueba. unit 336/336.
- minor aceptados: dos turnos simultáneos en una conversación → gana el último en la sesión (ya aceptado en plan §6, M7, token `xmin` como mitigación futura); la propiedad "nada se guarda tras un fallo" de punta a punta la cubren T031/T033.
## Rama PR4 — 2026-11-12
- `feature/maf-tool-calling-agent-pr4` creada por decisión del usuario, apilada sobre la pr3 (`70190f9`). Alcance: T025–T030.

## T025–T026 — 2026-11-12
- status: complete
- red: CS0234/CS0246 (`MafAgentReadTools` ausente). green: 32/32; unit 368/368; integración 11/11.
- mutantes: 14, todos detectados (2 tras añadir casos).
- decisiones: `MafAgentReadTools` (internal sealed, `Infrastructure/MafAgent/Tools`) separado de las herramientas de propuesta (T028); `MafAgentTools` las compondrá. Las 5 heredadas replican el código del plugin SK (no se comparte: `*/Agent/` no se toca) con `TimeProvider` en vez de `DateTime.UtcNow` y `CultureInfo.InvariantCulture` en los límites opcionales del pico (el plugin usa la cultura actual; en `yyyy-MM-dd` coinciden). Errores como texto para el modelo: `NotFound`, `InvalidDates` (el plugin SK lanza `ArgumentException`). El resto usa las consultas existentes por `ISender` recorriendo todas las páginas (límite 100). `ListExchanges` no expone `ApiKey`; `ListNotes` no expone el contenido. Records de salida propios (`Maf*`), no los del plugin SK.
- files: src/Infrastructure/MafAgent/Tools/MafAgentReadTools.cs, tests/WebApi.MinimalAPI.Tests/MafAgent/Tools/MafAgentReadToolsTests.cs
- verify: PASS (verifications/maf-tool-calling-agent-2026-11-12-T025-T026.md)
## T025–T026 — revisión por bloque — 2026-11-12
- status: complete
- review: VERDICT pass, sin hallazgos importantes. Comprobado: los handlers de entries, history y allocation filtran por dueño; ni la `ApiKey` del exchange ni el contenido de las notas del listado llegan al modelo; la paginación no se queda en bucle.
- minor corregido: el test de historial no comprobaba la fecha de cada punto (ahora sí, en el commit de T027–T028).
- minor aceptados: el pico usa cultura invariante (el plugin SK la actual; coinciden en `yyyy-MM-dd`); un error de validación de una consulta se devuelve al modelo como `NotFound`; los listados no tienen tope (el plan no lo pide).

## T027–T028 — 2026-11-12
- status: complete
- red: CS0246 (`MafAgentProposeTools` ausente). green: 43/43; lectura 32/32; unit 411/411; integración 11/11.
- mutantes: 19, todos detectados (2 tras añadir tests).
- decisiones:
  - **Desviación del plan §0 "Payloads tipados":** las fechas de las herramientas son `string`, no `DateTimeOffset`. System.Text.Json leería una fecha sin offset con la zona del SERVIDOR, y la regla es Madrid. La herramienta interpreta: con `Z`/offset ese instante; sin offset hora de Madrid (una hora inexistente por el cambio de verano → `MissingOrAmbiguous`). El payload sigue con `Kind=Utc`.
  - Parámetros nullable: un dato que falta da `MissingOrAmbiguous:<campo>` en vez de un error de enlace de argumentos. En las ediciones, un valor en blanco cuenta como "no cambia".
  - `Before` = la salida de `ICurrentValuesReader`, la misma fuente que la comprobación de desfase al confirmar; la fusión parsea esos valores ("G29", "O").
  - Dueño por `GetPortfolioByIdQuery` (ya lo comprueba): las herramientas no usan repositorios de negocio.
  - `BeginTurn(conversationId)` lo llamará el runner (T030) antes de cada turno; `ProposedActions` son las instancias preparadas.
  - Paginación compartida en `ToolPaging` (refactor de las herramientas de lectura, sin cambio de comportamiento).
- files: src/Infrastructure/MafAgent/Tools/{MafAgentProposeTools,ToolPaging,MafAgentReadTools}.cs, tests/WebApi.MinimalAPI.Tests/MafAgent/Tools/{MafAgentProposeToolsTests,MafAgentReadToolsTests}.cs
- verify: PASS (verifications/maf-tool-calling-agent-2026-11-12-T027-T028.md)
## T027–T028 — revisión por bloque — 2026-11-12
- status: complete
- review: VERDICT pass, sin hallazgos importantes. Comprobado: el `Before` recién propuesto no sale `Stale` al confirmar (mismo lector y formato), "0.50" frente a "0.5" no es un cambio falso, ningún camino de fallo prepara nada, dueño por consultas con ámbito de usuario.
- corregido (menor #3, fallo real): en `ProposeSendStatusReport` una fecha sin hora ("2026-09-30") se leía como medianoche de Madrid = 22:00 UTC del día anterior, y el informe usa `.Date` UTC → se perdía el último día. Ahora un límite de periodo con solo fecha es ese día a las 00:00 UTC, igual que el periodo por defecto (`TryParsePeriodBound`). Test rojo sin el arreglo.
- corregido: mutantes supervivientes (edición con cantidad 0, precio negativo, hora del hueco de verano) y huecos de test (precio 0 al crear, ids ausentes). 50/50; unit 418/418; integración 11/11.
- minor aceptados / para el usuario: (1) en una edición, un valor en blanco = "no cambia": no se puede vaciar una descripción por el agente; (2) hora ambigua del cambio de otoño → horario estándar; fechas no ISO se leen en cultura invariante (la descripción pide ISO); (4) longitudes no se validan al proponer (al confirmar sería `Failed`).
- abierto para el usuario: en `ProposeCreateEntry`/`ProposeUpdateEntry`, una fecha SIN hora ("2026-11-10") se lee como medianoche de Madrid = 23:00 UTC del día 9, y Domain agrupa las entradas por día UTC. Sigue la regla del plan ("sin offset → Madrid"), pero puede mostrar la entrada un día antes.
## T029–T030 — 2026-11-12
- status: complete
- red: CS0234/CS0246 (`Infrastructure.MafAgent.Runner` ausente). green: runner 14/14, fábrica 4/4, DI 2/2; unit 439/439; integración 11/11.
- mutantes: 17; 16 detectados (1 tras fijar el número de llamadas al modelo) y 1 que no compila.
- decisiones:
  - **Límite de pasos:** el middleware de invocación de funciones (`AIAgentBuilder.Use`) cuenta las llamadas del turno. Al pasar el límite no ejecuta la herramienta, pone `Terminate` y deja una marca. El runner lanza `AgentLoopExhaustedException` al terminar `RunAsync`. No se lanza desde dentro del middleware: no depende de si `FunctionInvokingChatClient` captura excepciones de herramientas.
  - Contexto horario: un mensaje de sistema por turno, antes del mensaje del usuario (queda en el historial de la sesión).
  - `AppendSystemNoteAsync`: añade un mensaje de sistema con `TryGetInMemoryChatHistory`/`SetInMemoryChatHistory`, sin llamar al modelo.
  - Fábrica del cliente: modelo de razonamiento de `AiOptions`, transporte del `HttpClient` "InferenceClient" (el timeout de Polly ya registrado para SK). Azure sin endpoint → `InvalidOperationException`.
  - Log de fallo de herramienta: solo el TIPO de excepción, vía filtro de excepción que registra y devuelve false (no hay `catch` de `Exception`).
  - **Defecto de T028 corregido aquí:** los parámetros nullable de `Propose*` no tenían `= null`, así que `AIFunctionFactory` los marcaba obligatorios y un dato omitido fallaba al enlazar en vez de llegar como `MissingOrAmbiguous:<campo>`. Test nuevo que invoca el `AIFunction` sin `pricePerUnit`.
  - DI: `AddMafAgent()` en Infrastructure (herramientas, fábrica y runner scoped; `IMafAgentRunner` → `MafAgentRunner`). `*/Agent/` sin cambios.
- files: src/Infrastructure/{DependencyInjection.cs,MafAgent/Runner/MafAgentRunner.cs,MafAgent/Runner/MafChatClientFactory.cs,MafAgent/Tools/MafAgentProposeTools.cs}, tests/WebApi.MinimalAPI.Tests/MafAgent/{Runner/*,Tools/MafAgentProposeToolsTests.cs}
- verify: PASS (verifications/maf-tool-calling-agent-2026-11-12-T029-T030.md)
## T029–T030 — revisión por bloque + decisión de fechas — 2026-11-12
- status: complete
- review: VERDICT fail, 1 important (reproducido por el revisor con una sonda real).
- **important #1 corregido (AC14):** el cliente de OpenAI tenía su propio timeout de red de 100 s y 3 reintentos, y el `HttpClient` su propio `Timeout` de 100 s. Con `Ai:RequestTimeoutSeconds` = 240 (Development) la petición se cortaba a los 100 s, se reintentaba (unos 400 s en total) y salía como `TaskCanceledException` → 500, no como `MafAgent.Timeout`. Ahora: `HttpClient.Timeout`, `NetworkTimeout` infinitos y `ClientRetryPolicy(0)` (OpenAI y Azure): solo el pipeline de Polly de "InferenceClient" corta y reintenta. El runner trata como timeout `TimeoutRejectedException`, `TimeoutException` y cualquier cancelación que el llamador NO pidió; una cancelación pedida se propaga tal cual. El agente SK tiene el mismo hueco, pero está congelado (no se toca).
- minor #2 corregido: el mensaje de contexto horario se acumulaba en el historial (varias "horas actuales" contradictorias). Ahora solo queda el del turno actual (`DropPreviousTimeContexts`).
- minor #4 corregido: Ollama sin `Endpoint` → error de configuración (antes llamaba a api.openai.com en silencio).
- mutantes supervivientes cubiertos: mapeo del puerto `IMafAgentRunner` → `MafAgentRunner` en producción (`AddMafAgent` pasa a `internal` para probarlo), uso del transporte y ausencia de reintentos del cliente, log de una herramienta que falla (solo el tipo de excepción).
- minor #3 aceptado: los parámetros `= null` aparecen como opcionales en el esquema; encaja con `MissingOrAmbiguous`.
- **Decisión del usuario (2026-11-12):** una fecha SIN hora que llega a una herramienta es ese día a las 00:00 UTC (entradas y periodo del informe), no la medianoche de Madrid. Una regla única en `TryParseInstant`; se elimina `TryParsePeriodBound`. Anotada en plan §0 "Payloads tipados" y en `harness/decisions.md`.
- 10 mutantes de los arreglos, todos detectados. unit 449/449; integración 11/11.