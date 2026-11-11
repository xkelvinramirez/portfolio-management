---
type: plan
title: Plan — Agente MAF con tool-calling
description: Plan a nivel de estructura del agente MAF con herramientas tipadas, conversaciones persistidas y acciones pendientes confirmadas fuera del modelo.
tags: [sdd, plan, agent, tool-calling, maf]
timestamp: 2026-11-11T14:00:00Z
topic: sdd
slug: maf-tool-calling-agent
status: draft
---

# Plan — Agente MAF con tool-calling

> Spec: [../specs/maf-tool-calling-agent.md](../specs/maf-tool-calling-agent.md) · Constitution: [../constitution.md](../constitution.md) (borrador; canon de facto = `CLAUDE.md` raíz) · Status: approved (usuario, 2026-11-11: "apruebo el plan"); revisado tras analyze ×4; GATE: PASS (ejecución 4) con MEDIUM resueltos o aceptados
> Last updated: 2026-11-11
>
> **Cambios tras [analyze](../analysis/maf-tool-calling-agent.md) (2026-11-11), a petición del usuario:**
> - F1: el desfase es un `200` con `Status=Stale` y nunca hay error tras el reclamo (§0, §3, §4 B.4, T019).
> - F2: `ResultJson` cifrado (§0, §4, T019).
> - F3: `ICurrentValuesReader` + firma de `StaleCheck` (§2, §3, T015/T016 + Interfaces T016/T020).
> - F4: el runner devuelve las instancias preparadas, y se elimina `GetByIdsAsync` (§3, T008, T023, T026/T028).
> - F5: sin `Idempotency-Key` en chat, aceptado por el usuario con la justificación corregida (§3, §7).
> - F7: test de selección por proveedor (§5, T029).
> - F8: SK 1.81.0 intocable sin aprobación (§0, §7, T001, T034).
> - F9: instrucciones de idioma, no soportado y dato faltante (§0, §5, T029, T035).
> - F10: T002 deja de ser `[P]` y depende de T001.
> - F12: nombres de tabla fijados con `ToTable` (T009, T010).
> - F13: recuento de 20 herramientas.
> - F14: checklist manual ampliada (T035).
> - F15: caso "sigue confirmable" en T019.
> - F16: referencia de tests corregida (§5).
> - Sin cambios: F6 y F11 (constitución) y F17 (config); quedan aceptados o pendientes de ratificar.
>
> **Cambios tras analyze, ejecución 2 (2026-11-11), a petición del usuario:**
> - G1: las ediciones guardan la petición completa, fusionada con los valores actuales, y `Before` cubre todos los campos (§0, §3, T013, T015, T017, T027).
> - G2: nuevo puerto `IAddedEntityIdLog` (interceptor de EF) para obtener el id real tras el commit (§3, §4, T017, T033 y las nuevas T037/T038).
> - G3: repositorio de test con transición condicional en memoria (T002).
> - G4: reject atómico con `TryRejectAsync` (§3, T011, T012, T021).
> - G5: reglas de visibilidad + `InternalsVisibleTo` (§0, T008, T009).
> - G6: zona horaria de negocio `Europe/Madrid` inyectada en el contexto, y `Summary` con hora local (§0, T013, T029).
> - G7: el periodo del informe se resuelve al proponer (§3, T027).
> - G8: tras el reclamo se aplican `MarkExecuting` y `CancellationToken.None`, y se captura `Exception` en un único punto, con la desviación documentada (§0, §4, §7, T019, Interfaces T020).
> - G10: resolución de nombre a id en el catálogo (§0, T027).
> - G12: invariante de DbContext compartido (§0).
> - G9 y G11 siguen abiertos (constitución y config).
>
> **Cambios tras analyze, ejecución 3 (2026-11-11), a petición del usuario:**
> - H1: AC1 cubre 4 preguntas; se elimina la herramienta de comparación entre fechas (spec actualizada; §3, §5, T025, T035; 19 herramientas).
> - H2: `PendingActionFactory`, `ICurrentValuesReader` y `StaleCheck` son `public` (§2, T016).
> - H3: en la rama de excepción, `ITrackedChangesDiscarder.DiscardAll()` + `TryMarkFailedAsync` atómico (§0, §3, §4, T011, T012, T019, T020).
> - H4: dependencias corregidas (T002 ← T006/T008/T009; T031 ← T009/T030/T038; T037 ← T002) y paquete InMemory en T002.
> - M1: host de test sin dependencias externas y sin cambios en producción (T002).
> - M2: validadores `public` y registro DI de Application (§0, T024).
> - M3: una sola firma del runner; el handler cifra y descifra (§2, §3, §4).
> - M4: redacción en `LoggingBehavior` (§0; nuevas T040/T041).
> - M5: payloads tipados y fechas round-trip `"O"` (§0, T016).
> - M6: el done-check de T008 usa `dotnet list package`.
> - M7: red de seguridad del agente SK (nueva T039, antes de T001) y vigilancia de `Microsoft.Extensions.AI*` (T001/T034).
>
> **Cambios tras analyze, ejecución 4 (GATE: PASS) (2026-11-11), a petición del usuario. Solo `tasks` y §0, sin re-analyze:**
> - M1: confirm se envía sin token y el commit externo no se cancela (§0).
> - M2: las fechas de las herramientas son `DateTimeOffset` y se normalizan a UTC; sin offset se interpretan en Madrid (§0, T027).
> - M3: `Summary` se redacta en los logs y no repite texto libre (§0, T027, T040).
> - M4: T031 depende de T012.
> - M5: firma real de `AddApplication` y dobles en T024.
> - M9: el grep de T034 solo mira líneas `^[+-]`.
> - M10: middleware contadora para el límite de iteraciones (§0, T029, Interfaces T030).
> - L2: el doble de T002 implementa también `TryMarkFailedAsync`.
> - L3: la factoría de T002 vuelve a añadir el interceptor.
> - **Aceptados por el usuario:** M6, M7, M8 y L1 (§7).

## 0. Global Constraints

Valores exactos que toda tarea respeta. Un implementador aislado solo ve su tarea y este bloque.

- **Runtime:** .NET 10, C# 12. `internal sealed` por defecto. Constructores primarios para DI. `record` para datos inmutables. `is null` / `is not null`. `var` solo cuando el tipo es evidente.
- **Capas:** Clean Architecture. `Domain` ← `Application` ← `Infrastructure` / `WebApi.MinimalAPI`. `Contracts` lo referencian `Application` y `WebApi`. **`Application` y `Domain` no referencian ningún paquete de MAF ni de `Microsoft.Extensions.AI`.**
- **Nombre de feature:** `MafAgent`, en espejo en `Application/MafAgent`, `Contracts/MafAgent`, `Infrastructure/MafAgent` y `WebApi.MinimalAPI/Endpoints/MafAgent`. **No se toca nada bajo `*/Agent/`** (agente SK), salvo leerlo.
- **Rutas:** grupo `/api/v{version:apiVersion}/MafAgent/`, versión 1, `RequireAuthorization()`. Registro en `EndpointRegistrar`, nunca en `Program.cs`.
- **Errores:** los handlers devuelven `ErrorOr<T>`. Nunca lanzan excepciones por fallos de negocio. Los endpoints traducen con `ToProblemResult()`. No se captura `Exception` a secas.
- **Códigos de error (exactos):** `MafAgent.Conversation.NotFound` (NotFound), `MafAgent.Conversation.Expired` (Conflict), `MafAgent.PendingAction.NotFound` (NotFound, también para la acción de otro usuario), `MafAgent.PendingAction.Expired` (Conflict), `MafAgent.PendingAction.AlreadyDecided` (Conflict), `MafAgent.LoopExhausted` (Failure), `MafAgent.Timeout` (Failure). **El desfase (AC18) no es un error HTTP:** es un resultado `200` con `Status=Stale`, porque la acción ya se reclamó y su estado final debe persistirse (ver §0 Commit).
- **Commit:** los handlers nunca llaman a `SaveChangesAsync`. Preparan cambios vía repositorio y `UnitOfWorkBehavior` hace el commit de las peticiones que implementan `ICommand`. **Únicas excepciones permitidas:** `IPendingActionRepository.TryClaimAsync`, `TryRejectAsync` y `TryMarkFailedAsync`, actualizaciones condicionales atómicas (ver §3). **Regla derivada:** después de un `TryClaimAsync` con resultado `true`, `ConfirmPendingActionCommand` **nunca devuelve un error `ErrorOr`**. Todo desenlace (`Succeeded`, `Failed`, `Stale`) se devuelve como `200` con su `Status`, para que la UoW persista el estado final.
- **Agente SK intocable:** no se modifica código bajo `*/Agent/` **ni la versión del paquete `Microsoft.SemanticKernel`** (1.81.0) sin aprobación explícita del usuario.
- **Visibilidad:** son `public` los tipos que cruzan proyectos: entidades de Domain, contratos, puertos de Application implementados en Infrastructure (`IMafAgentRunner`, `IMafConversationRepository`, `IPendingActionRepository`, `IAddedEntityIdLog`) y los comandos MediatR. Las implementaciones son `internal sealed`. `Application` e `Infrastructure` declaran `InternalsVisibleTo` para `Application.Tests`, `WebApi.MinimalAPI.Tests` y `DynamicProxyGenAssembly2` (NSubstitute).
- **Ediciones = petición completa:** el payload de todo `Update*` es la **petición completa** que espera el comando existente, ya fusionada con los valores actuales (`UpdatePortfolioRequest`: Name, Description; `UpdatePortfolioEntryRequest`: CryptoCurrencyId, ExchangeId, Quantity, PricePerUnit, RecordedAt; `UpdateNoteRequest`: Category, Title, Content, **IsActive**). `Before` contiene **todos** esos campos. Lo que se muestra al usuario como diferencia es solo una vista; lo que se ejecuta y se compara es la petición completa.
- **Catálogo nombre→id:** `symbol` se resuelve contra `GetCryptoCurrenciesQuery` comparando `Symbol` sin distinguir mayúsculas (`OrdinalIgnoreCase`). `exchangeName` se resuelve contra `GetExchangesQuery` comparando `Name` recortado, también sin distinguir mayúsculas. Ninguna coincidencia → `UnknownCatalogItem:<valor>`. Más de una → `MissingOrAmbiguous:<campo>`.
- **DbContext compartido:** confirm y el comando interno comparten el `AppDbContext` scoped. Esto es seguro porque **ningún handler de comando prepara cambios antes de devolver un error**, como ya ocurre en los existentes. Un handler nuevo o modificado que rompa esta regla rompe AC10. Las **excepciones** del comando interno, en cambio, sí pueden dejar cambios en el tracker. Por eso se tratan con el descarte de "Tras el reclamo" (3a).
- **Registro y validadores:** los validadores FluentValidation nuevos son `public sealed`, porque `AddValidatorsFromAssemblyContaining` ignora los tipos internos. Application registra en su `DependencyInjection` (scoped): `PendingActionFactory`, `ICurrentValuesReader → CurrentValuesReader` y `PendingActionExecutor`. Infrastructure registra (scoped): los repositorios, `IAddedEntityIdLog`, `ITrackedChangesDiscarder` e `IMafAgentRunner`.
- **Logs de MediatR:** `LoggingBehavior.SensitivePropertyNames` se amplía con `Message`, `Reply`, `Values`, `Before`, `Result`, `Content` y `Summary`. Además, `PendingActionDto.Summary` describe la operación y los campos que cambian sin repetir texto libre de usuario (títulos o contenido de notas, descripciones): por ejemplo "Editar nota 3f2a…: cambia Title, Content" (analyze ejecución 4, M3). Así, el texto del chat, los payloads descifrados, los resultados y el contenido de las notas nunca llegan a Seq (analyze M4). El cambio es transversal: también oculta `Content` en los logs actuales de Notes, lo que es deseable.
- **Payloads tipados:** `PayloadJson` y `BeforeJson` serializan con System.Text.Json la petición tipada (`Update*Request`, `Create*Request`). Las fechas van en formato round-trip `"O"` (precisión completa) y se leen con `DateTimeStyles.RoundtripKind`, dando `Kind=Utc`. **Toda fecha que entra por una herramienta se declara `DateTimeOffset`** (`recordedAt`, `periodStart`, `periodEnd`). Se normaliza con `.UtcDateTime` antes de construir el payload. Si llega sin offset, se interpreta en `Europe/Madrid`. Nunca entra al payload un `DateTime` con `Kind` `Unspecified` o `Local`, porque Npgsql lo rechaza sobre `timestamptz` (ver `PortfolioEntryRepository.cs:34-39`; analyze ejecución 4, M2).
- **Límite de iteraciones:** el runner registra en el agente una middleware de función que **cuenta** las invocaciones de herramientas del turno. Cuando el contador supera `AiOptions.MaxAllowedToolLoops`, lanza `AgentLoopExhaustedException`. No basta con `FunctionInvokingChatClient.MaximumIterationsPerRequest`, porque al llegar al límite corta sin lanzar. Si se usa, se fija por encima del contador para que la middleware actúe primero (analyze ejecución 4, M10). El snapshot de `ICurrentValuesReader` usa el mismo formato `"O"` y decimales `"G29"`. El formato a segundos (`yyyy-MM-ddTHH:mm:ssZ`) es **solo de presentación** en `Values`/`Summary` (analyze M5).
- **Tras el reclamo:** (1) la entidad en memoria pasa a `Executing` (`MarkExecuting`) para mantener las invariantes de Domain; (2) el ejecutor corre con `CancellationToken.None`, para que una desconexión del cliente no corte una escritura a medias. Además, el endpoint `confirm` envía `ConfirmPendingActionCommand` **sin token** (`sender.Send(command)`, es decir `CancellationToken.None`, como hacen todos los endpoints actuales). Así el commit externo de la UoW, que persiste el estado final, tampoco se cancela (analyze ejecución 4, M1); (3) **excepción documentada al canon** (`CLAUDE.md`: "no capturar `Exception` a secas"): en ese único punto, `ConfirmPendingActionCommandHandler` captura `Exception`, la registra en el log y entonces: (a) llama a `ITrackedChangesDiscarder.DiscardAll()` para vaciar el tracker, de modo que nada de lo que dejó el comando interno pueda guardarse después (analyze H3); (b) persiste el desenlace con `IPendingActionRepository.TryMarkFailedAsync(id, "MafAgent.PendingAction.UnexpectedFailure", now)`, una actualización atómica `Executing → Failed` fuera de la UoW; (c) devuelve `200` con `Status=Failed`. La nota de resultado en la sesión **no** se añade en esta rama: con el tracker vacío, la UoW externa no guarda nada. Motivo: después de un reclamo irreversible, el desenlace tiene que quedar persistido.
- **Identidad:** ninguna herramienta del agente recibe `userId` como parámetro. La identidad sale siempre de `ICurrentUserProvider.UserId`.
- **Tiempos:** acción pendiente válida **10 min** desde su creación. Conversación válida **24 h** desde `LastActivityAt`. Todo `DateTime` en UTC, obtenido de `TimeProvider` (inyectado, nunca `DateTime.UtcNow` en código nuevo).
- **Zona horaria de negocio:** `Europe/Madrid`, obtenida con `TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid")`, la misma que ya usan `RefreshPortfolioPricesCommandHandler` y `PortfolioStatusReportBackgroundService`. Las fechas relativas ("ayer", "el lunes") se resuelven en esa zona.
- **Fechas mostradas al usuario:** `PendingActionDto.Values` usa ISO-8601 UTC con `Z`, por ejemplo `2026-11-10T18:30:00Z`. `PendingActionDto.Summary` muestra además la hora local de Madrid con su offset, por ejemplo `2026-11-10T19:30:00+01:00 (Europe/Madrid)` (spec AC19). Las herramientas reciben y guardan siempre instantes UTC.
- **Límites del agente:** iteraciones de herramientas = `AiOptions.MaxAllowedToolLoops` (5 por defecto). Timeout de inferencia = `AiOptions.RequestTimeoutSeconds` (100 por defecto) vía el `HttpClient` `"InferenceClient"` existente. Se reutilizan `AgentLoopExhaustedException` y `AgentTimeoutException` del Domain.
- **Cifrado:** el estado de sesión persistido, `PendingAction.PayloadJson`, `PendingAction.BeforeJson` y `PendingAction.ResultJson` se cifran con `IEncryptionService` (AES existente), igual que las notas. Ninguna columna de texto de estas dos tablas guarda contenido de usuario en claro.
- **Tablas:** `mafagent_conversations` y `mafagent_pending_actions`, fijadas con `ToTable` en la configuración EF.
- **Instrucciones del agente:** (1) responder en el idioma del último mensaje del usuario; (2) ante peticiones de borrado o de escritura en catálogos, explicar que no está soportado en esta versión, sin proponer nada; (3) si una herramienta `Propose*` devuelve `MissingOrAmbiguous:<campo>`, preguntar por ese campo; (4) resolver las fechas relativas en `Europe/Madrid` a partir de la hora local inyectada y pasar a las herramientas el instante UTC equivalente. El runner inyecta en cada turno: la hora UTC actual, la hora local de `Europe/Madrid` con su offset y el id de zona.
- **Logs:** el log de herramientas registra nombre, conversación, duración y éxito o fallo. **Nunca** registra argumentos ni resultados de herramientas, ni texto de mensajes.
- **Secretos:** solo por `dotnet user-secrets`. Configuración de modelo reutilizando la sección `Ai` (`AiOptions`).
- **Tests:** xUnit. `NSubstitute` para dobles. `FluentAssertions`. `FakeTimeProvider` para el tiempo. Los tests de criterios deterministas (AC2–AC10b, AC13, AC15–AC19) **no** llaman a ningún modelo real.
- **Formato:** `dotnet format` y `dotnet build` limpios. El hook `after-cs-edit.sh` lo hace por archivo. `dotnet test` en verde antes de cerrar cada tarea.

## 1. Context & constraints

- **Criterios que moldean el diseño:** AC4 (la confirmación nunca pasa por el modelo), AC5 (como mucho una vez, incluso en concurrencia), AC7/AC13 (aislamiento entre usuarios), AC15/AC16 (persistencia y caducidad), AC18 (detección de desfase), AC11 (el agente SK no cambia).
- **Notas:** sin dueño. Misma visibilidad que la API de notas (spec §Clarifications, decisión surgida en plan). Se escriben en Cosmos y hacen commit directo (`CLAUDE.md` §Notes exception).
- **Constitución:** borrador. Se usa `CLAUDE.md` como canon (spec §Points to clarify, suposición tomada). Riesgo registrado en §7.
- **Fuera de alcance que el diseño NO debe tocar:** borrado, escritura de catálogos, frontend, streaming, retirada del agente SK, acciones agrupadas todo-o-nada, comportamiento dependiente del proveedor.
- **No funcional:** usuario único, una instancia de API. Latencia dominada por el modelo. No hay objetivo numérico de latencia en la spec (AC14 solo exige error claro al superar límites).

## 2. Architecture

```text
 HTTP (JWT)
   │
   ▼
[MafAgentEndpoints] ──ISender──► [Application/MafAgent]
   POST chat                        SendMafAgentMessageCommand ──► IMafAgentRunner (port)
   POST pending-actions/{id}/confirm  ConfirmPendingActionCommand ─┐         │
   POST pending-actions/{id}/reject   RejectPendingActionCommand   │         ▼
                                                                    │  [Infrastructure/MafAgent]
                                                                    │   MafAgentRunner ──► ChatClientAgent (MAF, EXTERNAL lib)
                                                                    │        │                  │
                                                                    │        │ tools            ▼
                                                                    │        ▼          IChatClient ─► Ollama / Azure OpenAI (EXTERNAL)
                                                                    │   MafAgentTools
                                                                    │    ├─ read tools ──ISender──► queries existentes / repos + Domain
                                                                    │    └─ propose tools ──► PendingActionFactory ──► IPendingActionRepository
                                                                    │
                                                                    └─► PendingActionExecutor ──ISender──► comandos existentes
                                                                              (CreatePortfolioEntryCommand, UpdateNoteCommand, …)
 Postgres: mafagent_conversations, mafagent_pending_actions      Cosmos: notes (existente)
```

- **MafAgentEndpoints** (internal) — traduce HTTP ↔ comandos MediatR. No tiene lógica.
- **SendMafAgentMessageCommand handler** (internal) — carga o crea la conversación, comprueba su dueño y su caducidad, delega en `IMafAgentRunner` y devuelve la respuesta más las acciones pendientes nuevas.
- **IMafAgentRunner / MafAgentRunner** (puerto en Application, adaptador en Infrastructure) — construye un `ChatClientAgent` por petición, restaura la sesión a partir del JSON **ya descifrado por el handler** (el runner nunca cifra ni descifra), ejecuta un turno y devuelve la sesión serializada. Es el único componente que conoce MAF.
- **MafAgentTools** (internal, Infrastructure) — las herramientas tipadas. Las de lectura delegan en queries o repositorios existentes. Las de propuesta (`Propose*`) validan y **solo crean una `PendingAction`**. Nunca mutan datos de negocio.
- **PendingActionFactory** (`public sealed`, Application; la consumen las herramientas de Infrastructure) — construye la `PendingAction` con su payload tipado, la instantánea "antes" (en ediciones) y su expiración.
- **ConfirmPendingActionCommand handler** (internal, Application) — reclama de forma atómica, comprueba el desfase y delega en `PendingActionExecutor`. Registra el resultado en la propia `PendingAction`, que hace de auditoría.
- **ICurrentValuesReader + StaleCheck** (Application; `ICurrentValuesReader` y `StaleCheck` son `public` porque los consumen las herramientas de Infrastructure, y la implementación `CurrentValuesReader` es `internal sealed`) — leen los valores actuales de un recurso editable mediante las queries existentes y los comparan con el snapshot `Before`. El mismo lector construye `Before` al proponer.
- **PendingActionExecutor** (internal, Application) — traduce cada `PendingActionOperation` al comando existente y lo envía por `ISender`. Así reutiliza validación, comprobación de dueño, invalidación de caché y commit de cada feature.
- **Repositorios** (`IMafConversationRepository`, `IPendingActionRepository`) — EF Core sobre `AppDbContext`.
- **Modelo / MAF** (EXTERNAL) — Ollama vía endpoint OpenAI-compatible, o Azure OpenAI, según `AiOptions.Provider`.

**Decisión arquitectónica principal: las acciones pendientes son de la app, no de MAF.** Las herramientas de escritura solo *proponen*. La ejecución ocurre en un comando aparte, disparado por un endpoint de confirmación, **sin que intervenga el modelo**.

- *Alternativa considerada:* `ApprovalRequiredAIFunction` / `ToolApprovalRequestContent` de MAF. Se descarta por cuatro motivos. (1) Al aprobar se reanuda una ejecución del modelo y la función corre dentro del bucle del modelo, lo que choca con AC4. (2) Las aprobaciones pendientes viven dentro del JSON de sesión, que no se puede consultar para garantizar AC5 (concurrencia), AC6 (expiración), AC7 (otro usuario) ni AC18 (desfase). (3) MAF no admite ejecuciones concurrentes sobre una misma sesión. (4) Los criterios AC2–AC10b, AC13 y AC15–AC19 no se podrían probar sin un modelo real.
- *Coste aceptado:* el modelo no "ve" el resultado de la ejecución en el mismo turno. Al confirmar, el servidor añade una nota al historial de la sesión ("acción X ejecutada: resultado"), para que el siguiente turno tenga contexto.

## 3. Interfaces & contracts

**HTTP (Contracts/MafAgent)**

```text
POST /api/v1/MafAgent/chat
  in : SendMafAgentMessageRequest { ConversationId: Guid?, Message: string (1..2000) }
  out: 200 SendMafAgentMessageResponse { ConversationId: Guid, Reply: string, PendingActions: PendingActionDto[] }
       404 MafAgent.Conversation.NotFound   (inexistente o de otro usuario — AC13)
       409 MafAgent.Conversation.Expired    (>24 h sin actividad — AC16)
       400 MafAgent.LoopExhausted | MafAgent.Timeout   (AC14)
  - ConversationId null → nueva conversación.
  - PendingActions contiene SOLO las creadas en este turno.

POST /api/v1/MafAgent/pending-actions/{id:guid}/confirm
  out: 200 PendingActionResultDto { Id, Operation, Status: Succeeded|Failed|Stale, Result: object?, Error: { Code, Description }? }
       404 MafAgent.PendingAction.NotFound      (inexistente u otro usuario — AC7)
       409 MafAgent.PendingAction.Expired | AlreadyDecided   (AC6, AC5)
  - Los errores 404/409 solo se devuelven ANTES del reclamo (o si el reclamo devuelve false).
  - Un fallo de negocio del comando subyacente → 200 con Status=Failed y Error (AC10).
  - Un desfase → 200 con Status=Stale y Error { Code: "MafAgent.PendingAction.Stale" } (AC18). El estado queda persistido.

POST /api/v1/MafAgent/pending-actions/{id:guid}/reject
  out: 204 | 404 NotFound | 409 Expired|AlreadyDecided

PendingActionDto { Id: Guid, Operation: string, Summary: string, Values: object, Before: object?, ExpiresAt: string(ISO Z) }
```

Ninguno de estos POST lleva `Idempotency-Key`. Es una **desviación aceptada por el usuario** (analyze F5, 2026-11-11) de la convención de `CLAUDE.md` sobre los POST de creación:
- `chat` **sí crea** una conversación (si `ConversationId` es null) y acciones pendientes. Un reintento de red las duplica. El duplicado es inocuo: nada se escribe sin una confirmación explícita por id, y las acciones caducan en 10 min y la conversación en 24 h.
- `confirm` y `reject` son idempotentes por id: el segundo intento recibe `AlreadyDecided` (AC5).

**Puerto del agente (Application → Infrastructure)**

```text
IMafAgentRunner.RunTurnAsync(conversation: MafConversation, decryptedSessionJson: string, message: string, ct)
IMafAgentRunner.AppendSystemNoteAsync(decryptedSessionJson: string, note: string, ct) -> string (sesión actualizada, sin cifrar)
  - El HANDLER descifra antes de llamar y cifra lo que el runner devuelve. El runner no conoce IEncryptionService (analyze M3).
    -> MafAgentTurnResult { Reply: string, SerializedSession: string(json, sin cifrar), ProposedActions: PendingAction[] }
  - ProposedActions son las MISMAS instancias preparadas con IPendingActionRepository.Add durante el turno (todavía sin commit).
    El handler construye los DTOs desde ellas. Nunca relee de BD, porque antes del commit no estarían.
  - throws AgentLoopExhaustedException | AgentTimeoutException (las traduce el handler, como hoy en SK)
  - precondición: conversation pertenece al usuario actual y no está caducada (lo comprueba el handler)
  - postcondición: ninguna mutación de datos de negocio. Solo PendingActions preparadas en el repositorio.
```

**Herramientas (MafAgentTools) — contratos lógicos**

```text
Lectura (sin efectos):
  ListMyPortfolios() -> PortfolioSummary[]
  GetPortfolioValue(portfolioId) -> decimal | NotFound
  GetPortfolioHoldings(portfolioId) -> Holding[] | NotFound                 (orden desc. por valor)
  GetAssetPeakOrTroughDate(portfolioId, symbol, extreme: max|min, from?, to?) -> AssetValuePoint | NotFound
  GetPortfolioEntries(portfolioId) -> Entry[] | NotFound
  GetPortfolioHistory(portfolioId, from?, to?) -> HistoryPoint[] | NotFound
  GetPortfolioAllocation(portfolioId) -> Allocation[] | NotFound
  ListCryptoCurrencies() / ListExchanges() -> CatalogItem[]
  ListNotes() -> NoteSummary[] ; GetNote(noteId) -> Note(content incl.) | NotFound
Propuesta (crea PendingAction, devuelve su resumen al modelo):
  ProposeCreatePortfolio(name, description)
  ProposeUpdatePortfolio(portfolioId, name?, description?)
  ProposeCreateEntry(portfolioId, symbol, exchangeName, quantity, pricePerUnit, recordedAtUtc)
  ProposeUpdateEntry(entryId, quantity?, pricePerUnit?, recordedAtUtc?, symbol?, exchangeName?)
  ProposeCreateNote(category, title, content)
  ProposeUpdateNote(noteId, category?, title?, content?)
  ProposeRefreshPrices(portfolioId)
  ProposeSendStatusReport(portfolioId, periodStart?, periodEnd?)
  -> PendingActionSummary { Id, Operation, Values, Before?, ExpiresAt } | MissingOrAmbiguous(field) | NotFound | UnknownCatalogItem(symbol|exchange)
  - ProposeUpdate*: los argumentos opcionales son solo los CAMBIOS. La herramienta lee el estado actual completo con
    ICurrentValuesReader, fusiona los cambios y guarda como payload la petición completa (§0 "Ediciones = petición completa").
    Before = snapshot completo. Ningún cambio respecto al actual → MissingOrAmbiguous:changes.
  - ProposeCreateEntry / ProposeUpdateEntry: symbol y exchangeName se resuelven a ids según §0 "Catálogo nombre→id".
  - ProposeSendStatusReport: si no llega periodo, la herramienta lo resuelve AL PROPONER con
    PortfolioStatusReportPeriod.PreviousCalendarMonth(utcNow), la misma función que usa el handler, y guarda las fechas concretas.
    El payload nunca lleva periodo nulo.
```

- Invariante: ninguna herramienta acepta `userId`. Los ids de portafolio y entrada se validan contra el dueño con la misma regla "NotFound si no es tuyo" que usan las queries existentes.
- Invariante: `Propose*` con datos incompletos o ambiguos **no** crea `PendingAction` (AC9). Con un activo o exchange desconocido tampoco (spec Behaviour §2).
- Invariante: no existe ninguna herramienta de borrado ni de escritura de catálogos (AC8). Las instrucciones del agente indican cómo responder a esas peticiones.
- Paridad (AC1): las **cuatro** herramientas de lectura heredadas (`ListMyPortfolios`, `GetPortfolioValue`, `GetPortfolioHoldings`, `GetAssetPeakOrTroughDate`) usan las mismas fuentes que el plugin SK de `development` (repositorios más el método de Domain `GetValueHistoryByAsset`). La comparación entre fechas está diferida en la spec y no se implementa. Los mismos datos entran y los mismos datos salen.

**Repositorios**

```text
IMafConversationRepository
  GetByIdAsync(id, ct) -> MafConversation?
  Add(conversation) ; Update(conversation)

IPendingActionRepository
  GetByIdAsync(id, ct) -> PendingAction?
  Add(action) ; Update(action)
  TryClaimAsync(id, userId, nowUtc, ct) -> bool
    - UPDATE atómico: Status Pending→Executing WHERE id=@id AND user_id=@userId AND status=Pending AND expires_at>@now
    - true si exactamente 1 fila. Se ejecuta de inmediato (fuera de la UoW): es la garantía de AC5.
  TryRejectAsync(id, userId, nowUtc, ct) -> bool
    - UPDATE atómico: Status Pending→Rejected, DecidedAt=@now, con el mismo WHERE que TryClaimAsync.
    - Así reject y confirm no se pisan: solo uno de los dos gana la transición desde Pending (analyze G4).
  TryMarkFailedAsync(id, errorCode, nowUtc, ct) -> bool
    - UPDATE atómico: Status Executing→Failed, ErrorCode, DecidedAt WHERE id=@id AND status=Executing.
    - Solo lo usa la rama de excepción de confirm, tras DiscardAll() (analyze H3).

ITrackedChangesDiscarder   (puerto en Application; implementación en Infrastructure con AppDbContext.ChangeTracker.Clear())
  DiscardAll() -> void

IAddedEntityIdLog   (puerto en Application; implementación: SaveChangesInterceptor scoped en Infrastructure)
  LastAddedId<TEntity>() -> long?     where TEntity : Entity
    - Devuelve el id de base de datos del último TEntity insertado por un SaveChanges en este scope.
    - Existe porque los handlers de creación construyen la respuesta ANTES del commit, así que su Id vale 0 (analyze G2).
```

**Ejecutor**

```text
PendingActionExecutor.ExecuteAsync(action, ct) -> ErrorOr<object>
  - CreatePortfolio → CreatePortfolioCommand ; UpdatePortfolio → UpdatePortfolioCommand
  - CreateEntry → CreatePortfolioEntryCommand ; UpdateEntry → UpdatePortfolioEntryCommand
  - CreateNote → CreateNoteCommand ; UpdateNote → UpdateNoteCommand
  - RefreshPrices → RefreshPortfolioPricesCommand (resultado con Updated/Skipped → AC10b)
  - SendStatusReport → TriggerPortfolioStatusReportCommand (comprueba el dueño)
  - Cada comando pasa por su propio pipeline (validación, caché, UoW o commit directo de Cosmos).
  - CreatePortfolio / CreateEntry: tras el éxito, el id del resultado se toma de IAddedEntityIdLog.LastAddedId<Portfolio|PortfolioEntry>(),
    NUNCA de la respuesta del comando. CreateNote no lo necesita, porque el id (Guid) se genera en Domain antes de guardar.
  - Se invoca siempre con CancellationToken.None (§0 "Tras el reclamo").
```

**Desfase (AC18)**

```text
FieldSnapshot = IReadOnlyDictionary<string, string?>   (campo → valor en forma invariante; decimales "G29", fechas ISO Z)

ICurrentValuesReader.ReadAsync(op: PendingActionOperation, targetId: string, ct) -> ErrorOr<FieldSnapshot>
  - UpdatePortfolio → GetPortfolioByIdQuery ; UpdateEntry → GetPortfolioEntryByIdQuery ; UpdateNote → GetNoteByIdQuery
    (si la respuesta de una query no expone algún campo de la petición completa, como IsActive o los ids de catálogo,
    se lee ese campo con el repositorio de la feature).
  - El snapshot contiene EXACTAMENTE los campos de la petición completa de §0 "Ediciones = petición completa".
  - NotFound se propaga. Tras el reclamo, el handler lo convierte en Status=Failed (no en error HTTP).
  - La misma lectura se usa al PROPONER para construir `Before`, así que los dos snapshots son comparables.

StaleCheck.IsStale(before: FieldSnapshot, current: FieldSnapshot) -> bool
  - true si algún campo de `before` tiene un valor distinto en `current`. Solo aplica a Update*.
```

## 4. Data model & flow

**Entidades (Domain)**

- **MafConversation** — `Id: Guid`, `UserId: long`, `EncryptedSessionState: string`, `CreatedAt`, `LastActivityAt`. Método `IsExpired(nowUtc)` → `nowUtc - LastActivityAt > 24h`. Pertenece a `User`.
- **PendingAction** — `Id: Guid`, `ConversationId: Guid`, `UserId: long`, `Operation: PendingActionOperation` (enum de 8), `PayloadJson: string`, `BeforeJson: string?`, `Status: PendingActionStatus` (`Pending | Executing | Succeeded | Failed | Rejected | Stale`), `CreatedAt`, `ExpiresAt` (= CreatedAt + 10 min), `DecidedAt: DateTime?`, `ResultJson: string?`, `ErrorCode: string?`. Métodos: `IsExpired(now)`, `MarkSucceeded/MarkFailed/MarkRejected/MarkStale(now, …)`. Pertenece a `MafConversation`.
  - Es el **registro de auditoría** (AC12): usuario, acción, operación, valores, cuándo y resultado. Además, cada invocación de herramienta se registra en Serilog con una middleware de función de MAF (nombre de la herramienta, conversación, duración; sin argumentos con contenido de notas).
  - `PayloadJson`, `BeforeJson` y `ResultJson` pueden contener contenido de notas. Los tres se cifran igual que la sesión (analyze F2).

**Flujo principal A — proponer** (spec Behaviour §Escritura 1-3)

1. `POST chat {conversationId?, message}` → `SendMafAgentMessageCommand` (ICommand).
2. El handler carga la conversación. Si no existe o es de otro usuario: `Conversation.NotFound`. Si `IsExpired`: `Conversation.Expired`. Si `conversationId` es null, crea una conversación nueva.
3. El handler descifra la sesión. `IMafAgentRunner.RunTurnAsync` la restaura con `DeserializeSessionAsync`, inyecta en el contexto la hora UTC, la hora local de `Europe/Madrid` y el id de zona, y ejecuta `RunAsync(message, session)`.
4. El modelo llama a `ProposeCreateEntry(...)`. La herramienta valida el dueño y el catálogo. `PendingActionFactory` crea la `PendingAction` (Pending, expira +10 min) y la prepara en el repositorio. La herramienta devuelve el resumen al modelo.
5. El runner serializa la sesión y la devuelve. El handler la cifra, actualiza `LastActivityAt` y devuelve `Reply` más los `PendingActionDto` creados en el turno.
6. `UnitOfWorkBehavior` hace el commit de la conversación y de las acciones. **Si el turno falla (AC14), devuelve error y no se hace commit de nada: no queda ninguna acción pendiente.**

**Flujo principal B — confirmar** (spec Behaviour §Escritura 4-6)

1. `POST pending-actions/{id}/confirm` → `ConfirmPendingActionCommand` (ICommand).
2. Carga la acción. Si no existe o es de otro usuario: `NotFound`. Si está caducada (`ExpiresAt` o la conversación caducada): `Expired`. Si no está en `Pending`: `AlreadyDecided`.
3. `TryClaimAsync`. Si devuelve false (carrera perdida con otro confirm o con un reject, o caducó entre medias): `AlreadyDecided`. Si devuelve true: `MarkExecuting` en memoria. Desde aquí, la acción no se puede ejecutar de nuevo (AC5).
4. En las acciones `Update*`, `ICurrentValuesReader` lee los valores actuales y se aplica `StaleCheck`. Si hay desfase, `MarkStale` y se devuelve **200 con `Status=Stale`** (AC18). Así la UoW persiste el estado. No se despacha ningún comando.
5. `PendingActionExecutor.ExecuteAsync(…, CancellationToken.None)` → comando existente por `ISender`. Su pipeline hace el commit del cambio de negocio. En las creaciones con identidad de BD, el id se obtiene de `IAddedEntityIdLog`. Si se lanza una excepción: `DiscardAll()` + `TryMarkFailedAsync` + `200` con `Status=Failed` (§0 "Tras el reclamo" 3a–3c).
6. `MarkSucceeded(result)` o `MarkFailed(errorCode)`. Se añade una nota de resultado a la sesión de la conversación. El commit lo hace la UoW externa.

- **Fronteras de consistencia:** el reclamo es atómico e inmediato. El cambio de negocio se confirma en el pipeline de su comando (Postgres) o directamente (Cosmos). El estado final de la acción se confirma después. Si el proceso cae entre 5 y 6, la acción queda en `Executing` para siempre: nunca se re-ejecuta (se prefiere "como mucho una vez" a "al menos una vez"). Se diagnostica con el log y con la entidad.
- **Migración:** EF Core, dos tablas nuevas `mafagent_conversations` y `mafagent_pending_actions` (nombres fijados con `ToTable`), índices por `user_id` y por `conversation_id`. Es aditiva y no destruye nada. Sin cambios en Cosmos.
- **Retención:** las conversaciones caducadas quedan inertes (rechazadas al acceder). Su purga física queda como decisión abierta (§7).

## 5. Testing strategy

Proyectos: `Domain.Tests` (entidades), `Application.Tests` (handlers con dobles) y `WebApi.MinimalAPI.Tests` (herramientas, runner e integración HTTP; referencia a Infrastructure vía WebApi). En esta rama `WebApi.MinimalAPI.Tests` solo tiene tests de idempotencia. La infraestructura de tests MafAgent se crea en T002.

| Criterio | Nivel | Qué afirma | Qué se falsea |
| --- | --- | --- | --- |
| AC1 | integration (WebApi.Tests) | Las 4 herramientas heredadas devuelven los mismos datos que los métodos homólogos de `PortfolioAgentPlugin` con el mismo seed | Repos en memoria / seed. Sin modelo. |
| AC1b | integration (WebApi.Tests) | Las herramientas de lectura nuevas devuelven datos del seed, y NotFound si no existen o son de otro usuario (excepto notas) | `INoteRepository` falso. Sin modelo. |
| AC2, AC9 | unit (WebApi.Tests, herramientas) | `Propose*` con datos completos crea 1 `PendingAction` sin mutar repos de negocio. Incompleto, ambiguo o catálogo desconocido → 0 acciones | Repos falsos. `FakeTimeProvider`. |
| AC3, AC10, AC10b | unit (Application.Tests) | Confirmar despacha el comando exacto con los valores del payload. Fallo → `Failed` sin cambio parcial. Refresh con Skipped → resultado con ambas listas | `ISender` falso (verifica el comando recibido) |
| AC4 | unit + integration HTTP | Ningún camino de `chat` invoca `PendingActionExecutor` ni comandos de escritura. Solo `confirm` lo hace | Runner falso que emite texto "sí/confirmo/<id>" |
| AC5 | integration (Postgres real o Testcontainers) | Dos `confirm` concurrentes → exactamente 1 ejecución | **Base real**: la garantía vive en el UPDATE condicional |
| AC6, AC16 | unit (Domain + Application) | Acción caducada (+10 min) o conversación caducada (+24 h) → `Expired`, ninguna ejecución | `FakeTimeProvider` |
| AC7, AC13 | unit (Application) | La acción o conversación de otro usuario → mismo error que un id inexistente | `ICurrentUserProvider` falso |
| AC8 | unit (configuración de herramientas) | El conjunto de herramientas registrado no contiene ninguna de borrado ni de escritura de catálogos | — |
| AC11 | regression | Hoy no hay tests del agente SK, así que T039 crea primero `SkAgentRegressionTests` en verde sobre `development` (construcción del kernel, plugin y filtros, más los datos de las 4 funciones). Esos tests y la suite completa siguen verdes al final (T034). Se vigila la versión transitiva de `Microsoft.Extensions.AI*` (T001/T034) | Seed, sin red |
| AC12 | unit (Application) | Tras confirmar, la `PendingAction` guarda usuario, operación, valores, `DecidedAt` y resultado o error | `ISender` falso |
| AC14 | unit (Application) | El runner lanza LoopExhausted o Timeout → error, no se prepara ni se hace commit de nada | Runner falso que lanza |
| AC15 | integration | Conversación + acción persistidas, nuevo `ServiceProvider` (simula reinicio) → se continúa y se confirma | Base real o InMemory. Runner falso. |
| AC17 | unit (herramientas) | 3 llamadas `Propose*` en un turno → 3 acciones independientes. Confirmar 2 aplica 2 | Repos falsos |
| AC18 | unit (Application) | Cambio externo tras proponer → `Stale`, ningún comando despachado | `ISender` falso para la lectura actual |
| AC19 | unit (factoría o herramienta) | Fecha relativa resuelta → `Values` muestra ISO `...Z` | `FakeTimeProvider` |
| Proveedor del modelo (Non-goal "mismo comportamiento") | unit (runner/factoría) | `Provider=Ollama/OpenAI` → cliente OpenAI-compatible con `Endpoint`; `Provider=AzureOpenAI` → cliente Azure con `DeploymentName`; las mismas herramientas e instrucciones en ambos | Sin red: solo construcción del cliente |
| Instrucciones (idioma, no soportado, dato faltante, fechas en Madrid) + AC19 | unit (runner) | El texto de instrucciones contiene las 4 reglas de §0; el contexto inyectado incluye hora UTC, hora local `Europe/Madrid` con offset e id de zona. La resolución de "ayer" la hace el modelo y se verifica en la checklist manual (T035) | `FakeTimeProvider` |
| Selección de herramientas con modelo real | e2e manual / spike (no CI) | Con qwen2.5:3b: las 4 preguntas de AC1; "registra 0.5 BTC…"; "borra el portafolio X" (rechazo, AC8); "registra una compra de BTC" sin precio (pide dato, AC9); una pregunta en inglés (responde en inglés) | Ollama real |

- **Línea e2e:** no hay e2e con modelo en CI. Es no determinista, lento y depende de Ollama. Se cubre con un spike medido (§7) y una lista de comprobación manual.
- **Dependencia que DEBE ser real:** Postgres en AC5. Un `InMemory` no reproduce la atomicidad del UPDATE condicional. Si no hay Testcontainers en el repo, el test usa el Postgres de desarrollo, marcado con un `Trait` de integración.
- **Cifrado:** test de ida y vuelta de la sesión (cifrar → persistir → descifrar → `DeserializeSessionAsync`) con la clave de test.

## 6. Sequencing & dependencies

1. **Spike MAF + modelo local**: un `ChatClientAgent` con 2–3 herramientas contra Ollama `qwen2.5:3b-instruct` y contra la configuración Azure. Se verifican la convivencia de versiones con `Microsoft.SemanticKernel 1.81.0` (versión común de `Microsoft.Extensions.AI`), la serialización de la sesión y el límite de iteraciones. Salida: versiones de paquete fijadas y una nota en `decisions.md`. — depende de: nada — serial (desbloquea 6)
2. **Domain:** `MafConversation`, `PendingAction`, enums y métodos de caducidad y transición + tests. — depende de: nada — paralelizable con 1 y 3
3. **Contracts:** DTOs de §3. — depende de: nada — paralelizable
4. **Infrastructure/persistencia:** configuraciones EF, `DbSet`, repositorios (incluido `TryClaimAsync`), migración. — depende de: 2 — serial
5. **Application/MafAgent:** puerto `IMafAgentRunner`, `PendingActionFactory`, `StaleCheck`, `PendingActionExecutor`, comandos Send/Confirm/Reject con validadores + tests unitarios (AC3–AC7, AC10–AC14, AC16, AC18). — depende de: 2, 3 (usa interfaces de repositorio, no la implementación) — paralelizable con 4
6. **Infrastructure/MafAgent:** factoría de `IChatClient` y agente (desde `AiOptions`), `MafAgentTools` (lectura + propuesta), `MafAgentRunner` (sesión cifrada, inyección de fecha, middleware de límite y log) + tests de herramientas (AC1, AC1b, AC2, AC8, AC9, AC17, AC19). — depende de: 1, 4, 5
7. **WebApi:** `MafAgentEndpoints` + registro + `EndpointTags` + tests HTTP (AC4, AC7, AC13). — depende de: 5 (6 para el chat real; los tests usan un runner falso)
8. **Integración:** AC5 (concurrencia con Postgres real), AC15 (reinicio), regresión AC11 (suite completa). — depende de: 4, 6, 7
9. **Documentación:** `docs/maf-agent-flow.md` (homólogo de `agent-chat-flow.md`) + lista de comprobación manual con modelo real. — depende de: 8

- **Candidatos a paralelo:** {2, 3} al principio. {4, 5} cuando 2 y 3 terminan. 1 en paralelo con 2–5.
- **Orden duro:** 1 antes de 6, porque si MAF no convive con SK o no serializa sesiones como se espera, cambia el adaptador. 4 antes de 8, por la migración.

## 7. Risks & open decisions

**Risks**

| Riesgo | Disparador | Impacto | Mitigación / spike |
| --- | --- | --- | --- |
| El modelo local (qwen 3b) elige mal entre 19 herramientas (11 de lectura + 8 de propuesta) | Preguntas de AC1 mal enrutadas en el spike | El agente no es usable con Ollama. El relevo de SK se frena | Spike (tarea 1) medido sobre las 4 preguntas + 2 escrituras. Si falla: descripciones más estrictas, agrupar lecturas o usar el modelo Azure como referencia. Resuelve el "área no formulable" de la spec |
| Conflicto de versiones `Microsoft.Extensions.AI` entre SK 1.81 y MAF | Error de compilación o carga en la tarea 1 | No conviven en el mismo proceso. AC11 en peligro | Spike primero. Fijar versiones de MAF compatibles con SK 1.81.0. **Subir la versión de SK modifica el agente actual (spec Non-goals): solo con aprobación explícita del usuario**, registrada en `decisions.md`, y con AC11 re-verificado |
| La API de MAF cambia (paquetes jóvenes) | Actualización de paquete | Rotura del adaptador | MAF aislado en `Infrastructure/MafAgent` tras `IMafAgentRunner`. Versión exacta fijada |
| Ventana de carrera en AC18 (cambio externo entre la comprobación y la escritura) | Edición desde la UI en el mismo milisegundo | Se pisa un cambio | Riesgo aceptado: usuario único. Mitigación futura: token de concurrencia (`xmin`) en las entidades |
| **Observado, fuera de alcance:** los `POST` de creación existentes (`Portfolio`, `PortfolioEntry`) probablemente devuelven `id: 0`, porque la respuesta se construye antes del commit | Usar la API HTTP actual | Clientes que reciben un id falso | No se corrige en esta feature (spec: alcance MafAgent). El agente lo evita con `IAddedEntityIdLog`. Se recomienda un FTD aparte que reutilice ese mismo puerto |
| Captura de `Exception` en confirm (desviación de `CLAUDE.md`) | Revisión contra el canon | Hallazgo en `review` | Un solo punto, justificado en §0 "Tras el reclamo", registrado en `decisions.md`. Debe ratificarse con la constitución |
| **Aceptado (analyze ejecución 4, M7):** escritura perdida en `EncryptedSessionState` si confirm y un turno de chat de la misma conversación coinciden | Dos peticiones simultáneas sobre una conversación | Se pierde la nota de resultado en la sesión o un turno; los datos de negocio y la auditoría no se ven afectados | Aceptado por el usuario (un solo usuario). Mitigación futura: token `xmin` en `MafConversation` |
| **Aceptado (analyze ejecución 4, M6/M8/L1):** contratos de `MafConversation` (factoría, sesión inicial, cómo llega `conversationId` a las herramientas, quién llama a `IPendingActionRepository.Add`), harness de integración con Postgres (cadena, siembra de FK, limpieza) y la visibilidad de `CurrentValuesReader` | Primera tarea que los produce (T004, T011, T014/T026) | Ambigüedad resoluble por el implementador | Aceptado por el usuario: los fija la tarea productora y los anota en su Interfaces/código. `review` los comprueba |
| Acción atascada en `Executing` si el proceso cae a mitad | Caída entre la ejecución y el marcado | La acción no refleja el resultado real | Aceptado (como mucho una vez). Visible en el log. Se resuelve a mano |
| Contenido de notas en logs | Middleware de log con argumentos | Fuga de texto cifrado en reposo hacia Seq | Global Constraint: el log no incluye argumentos ni resultados de herramientas de notas |
| La constitución sin ratificar cambia el canon | Fase `constitution` posterior | Hay que revisar plan y código | Canon = `CLAUDE.md`. Revisar §0 al ratificar |
| `POST chat` sin `Idempotency-Key` | Reintento de red | Conversación y acciones pendientes duplicadas | Aceptado por el usuario (analyze F5): son inocuas, porque requieren confirmación y caducan. Documentado en §3 |

**Open decisions**

- Purga física de conversaciones caducadas (hoy quedan inertes). Se cierra cuando se decida si `Worker` deja de ser un stub. No bloquea v1.
- Versiones exactas de los paquetes MAF / `Microsoft.Extensions.AI`. Se cierra en la tarea 1 (spike).

## Tasks
<!-- generated by tasks on 2026-11-11; IDs are stable, do not renumber -->

Convenciones de las comprobaciones de "hecho":
- `UNIT` = `dotnet test portfolio-management.slnx --filter "Category!=Integration&Category!=Manual"`. Es la suite rápida: sin Postgres, sin Cosmos y sin modelo.
- `INTEG` = `dotnet test portfolio-management.slnx --filter "Category=Integration"`. Necesita el Postgres de desarrollo, con la migración aplicada.
- "rojo" = el test existe, compila y **falla por la razón esperada**. La razón es que falta el comportamiento, no un error de compilación de otro módulo.
- Toda tarea de implementación cumple también dos cosas: `dotnet build portfolio-management.slnx` termina con código 0, y `UNIT` queda en verde sin regresiones.

| ID | [P] | Task | Done-check | Depends-on | Trace |
| --- | --- | --- | --- | --- | --- |
| T001 |  | Run spike: MAF `ChatClientAgent` + 2 tools against Ollama; construct Azure OpenAI client from config; pin package versions | `dotnet build portfolio-management.slnx` exit 0 con MAF y SK 1.81.0 referenciados a la vez (**sin cambiar la versión de SK**; si fuera necesario, PARAR y pedir aprobación al usuario); `UNIT` verde; `dotnet test tests/WebApi.MinimalAPI.Tests --filter "FullyQualifiedName~MafSpikeTests"` verde con Ollama levantado (Trait `Manual`; la clase vive en `tests/WebApi.MinimalAPI.Tests/MafAgent/Spike/`); ida y vuelta de sesión serializar→deserializar comprobada; el cliente Azure se construye sin llamada de red; nueva fila "MAF package versions" en `decisions.md`; `dotnet list src/Infrastructure/Infrastructure.csproj package --include-transitive` guardado antes y después en `docs/maf-agent-packages.md`, con cualquier cambio de versión `Microsoft.Extensions.AI*` señalado y aprobado por el usuario; T039 sigue verde | T039 | spec §Points (suposición MAF, área no formulable), AC11 |
| T002 |  | Add test infrastructure for MafAgent tests | `WebApi.MinimalAPI.Tests` referencia `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.Extensions.TimeProvider.Testing` y `Microsoft.EntityFrameworkCore.InMemory`; el host de test usa el entorno `Testing` (no carga user-secrets), una configuración en memoria con valores de prueba para `Jwt`, `Encryption:Key` (32 bytes base64) y las cadenas de conexión, `ApplicationInsights:ConnectionString` vacío (Azure Monitor se omite solo) y un doble NSubstitute de `CosmosClient` que completa la inicialización de `EnsureCosmosDbInitializedAsync`, todo sin tocar código de producción (analyze M1); `MafAgentApiFactory` (WebApplicationFactory) arranca con esquema de auth de test (user id por cabecera), `IMafAgentRunner` falso, `FakeTimeProvider`, EF InMemory y un `IPendingActionRepository` de test que delega el CRUD en EF InMemory e implementa `TryClaimAsync`/`TryRejectAsync`/`TryMarkFailedAsync` como transiciones condicionales en memoria con `lock` (analyze ejecución 4, L2). Al cambiar `DbContextOptions` a InMemory, la factoría **vuelve a añadir** el interceptor `AddedEntityIdLog` y registra `ITrackedChangesDiscarder`, para que `IAddedEntityIdLog` no devuelva `null` en T031 (L3) (InMemory no soporta `ExecuteUpdate`, analyze G3); `MafAgentApiFactorySmokeTests` verde en `UNIT` | T001 (mismo `.csproj`), T006, T008, T009 | plan §5 |
| T003 | [P] | Write failing tests for `MafConversation` | `dotnet test tests/Domain.Tests --filter "FullyQualifiedName~MafConversationTests"` rojo: tipo y métodos ausentes (`IsExpired` en 24 h exactas y en +1 tick; `Touch` actualiza `LastActivityAt`) | — | AC15, AC16 |
| T004 |  | Implement `MafConversation` | T003 verde; `UNIT` verde | T003 | AC15, AC16 |
| T005 | [P] | Write failing tests for `PendingAction` | `dotnet test tests/Domain.Tests --filter "FullyQualifiedName~PendingActionTests"` rojo: `Create` fija `ExpiresAt = CreatedAt + 10 min`; `IsExpired`; transiciones válidas solo desde `Pending`/`Executing`; `Mark*` fija `DecidedAt` y `ResultJson`/`ErrorCode` | — | AC5, AC6, AC12 |
| T006 |  | Implement `PendingAction`, `PendingActionOperation`, `PendingActionStatus` | T005 verde; `UNIT` verde | T005 | AC5, AC6, AC12 |
| T007 | [P] | Add MafAgent contracts | DTOs de §3 en `src/Contracts/MafAgent/`; `dotnet build` exit 0 | — | plan §3 |
| T008 |  | Add Application ports: repository interfaces + `IMafAgentRunner` | interfaces de §3 en `src/Application/MafAgent/Interfaces/` (incl. `IAddedEntityIdLog` e `ICurrentValuesReader`), todas `public`; `Application.csproj` declara `InternalsVisibleTo` para `Application.Tests`, `WebApi.MinimalAPI.Tests` y `DynamicProxyGenAssembly2`; `dotnet build` exit 0; `Application.csproj` sin referencias directas a MAF ni a `Microsoft.Extensions.AI`: la salida de `dotnet list src/Application/Application.csproj package` no contiene `Microsoft.Agents` ni `Microsoft.Extensions.AI` (analyze M6) | T004, T006, T007 | plan §0, §3 |
| T009 |  | Implement EF configurations, `DbSet`s and repositories (except `TryClaimAsync`) | `MafConversationConfiguration` y `PendingActionConfiguration` (`ToTable("mafagent_conversations")` / `ToTable("mafagent_pending_actions")`, columnas snake_case, índices por `user_id` y `conversation_id`); repos registrados en DI; `TryClaimAsync` y `TryRejectAsync` lanzan `NotImplementedException`; `Infrastructure.csproj` declara `InternalsVisibleTo` para `WebApi.MinimalAPI.Tests` y `DynamicProxyGenAssembly2`; `dotnet build` exit 0 | T008 | plan §4 |
| T010 |  | Add migration `AddMafAgent` | desde `src/`: `dotnet ef migrations add AddMafAgent --context AppDbContext --project Infrastructure --startup-project WebApi.MinimalAPI` genera solo 2 `CreateTable` más índices; `dotnet ef database update --project Infrastructure --startup-project WebApi.MinimalAPI` aplica limpio; existen `mafagent_conversations` y `mafagent_pending_actions` | T009 | plan §4 |
| T011 |  | Write failing integration tests for `TryClaimAsync` | `INTEG` con `--filter "FullyQualifiedName~PendingActionRepositoryClaimTests"` rojo: 2 claims concurrentes → exactamente 1 `true`; claim y reject concurrentes → exactamente uno `true` y estado final coherente; otro usuario → `false`; expirada → `false`; no-`Pending` → `false` (para ambos métodos); `TryMarkFailedAsync` solo actúa sobre `Executing` (sobre `Pending`/`Succeeded` → `false`) | T010 | AC5, AC6, AC7, AC12 |
| T012 |  | Implement `TryClaimAsync`, `TryRejectAsync` and `TryMarkFailedAsync` as atomic conditional updates, plus `ITrackedChangesDiscarder` | T011 verde; únicas escrituras fuera de la UoW: `grep -rn "ExecuteUpdate" src/Infrastructure/MafAgent` = 3 coincidencias, una por método; `TrackedChangesDiscarderTests` (EF InMemory) verde: tras `DiscardAll()` un `SaveChanges` no persiste nada | T011 | AC5, AC10, AC12 |
| T013 | [P] | Write failing tests for `PendingActionFactory` | `dotnet test tests/Application.Tests --filter "FullyQualifiedName~PendingActionFactoryTests"` rojo: payload para cada una de las 8 operaciones; en `Update*` el payload es la petición completa y `Before` contiene todos sus campos; `ExpiresAt` +10 min con `FakeTimeProvider`; fechas de `Values` en ISO `...Z`; `Summary` con la hora local de `Europe/Madrid` y su offset (`+01:00` en invierno y `+02:00` en verano); payload y before cifrados con `IEncryptionService` | T008 | AC2, AC19, plan §0 Cifrado |
| T014 |  | Implement `PendingActionFactory` | T013 verde | T013 | AC2, AC19 |
| T015 | [P] | Write failing tests for `StaleCheck` and `CurrentValuesReader` | `dotnet test tests/Application.Tests --filter "FullyQualifiedName~StaleCheckTests\|FullyQualifiedName~CurrentValuesReaderTests"` rojo: snapshots iguales → no hay desfase; cualquier campo de `before` distinto en `current` → desfase; el lector despacha `GetPortfolioByIdQuery`/`GetPortfolioEntryByIdQuery`/`GetNoteByIdQuery` según la operación y normaliza decimales a "G29" y fechas a ISO Z; el snapshot contiene exactamente los campos de la petición completa (incl. `IsActive`, `CryptoCurrencyId` y `ExchangeId`); un cambio externo en un campo que el usuario NO pidió cambiar también se detecta como desfase; NotFound se propaga | T008 | AC3, AC18 |
| T016 |  | Implement `StaleCheck` and `CurrentValuesReader` | T015 verde | T015 | AC18 |
| T017 | [P] | Write failing tests for `PendingActionExecutor` | `dotnet test tests/Application.Tests --filter "FullyQualifiedName~PendingActionExecutorTests"` rojo: cada operación despacha por `ISender` exactamente el comando de §3 con los valores del payload; refresh devuelve `Updated` y `Skipped`; `CreatePortfolio`/`CreateEntry` devuelven el id de `IAddedEntityIdLog` aunque la respuesta del comando traiga `Id=0`; `ProposeUpdateNote` de una nota inactiva despacha `IsActive=false`; se despacha con `CancellationToken.None`; un error del comando se propaga como error `ErrorOr` | T008 | AC3, AC10, AC10b |
| T018 |  | Implement `PendingActionExecutor` | T017 verde | T017 | AC3, AC10, AC10b |
| T019 |  | Write failing tests for `ConfirmPendingActionCommandHandler` | `dotnet test tests/Application.Tests --filter "FullyQualifiedName~ConfirmPendingActionCommandHandlerTests"` rojo: caso feliz → `Succeeded` + resultado + `DecidedAt`; de otro usuario o inexistente → el mismo `PendingAction.NotFound`; expirada o conversación con más de 24 h → `Expired`; ya decidida o `TryClaimAsync=false` → `AlreadyDecided`; edición con desfase → **resultado no-error** con `Status=Stale`, `MarkStale` aplicado a la entidad y 0 despachos; recurso borrado entre propuesta y confirmación → `Status=Failed`; fallo del comando → `Status=Failed` con `ErrorCode`; **tras un reclamo `true` el handler nunca devuelve `IsError`** (todas las ramas); tras el reclamo la entidad pasa por `MarkExecuting`; si el ejecutor lanza una excepción (incl. el `SaveChanges` del comando interno) → `ITrackedChangesDiscarder.DiscardAll()` invocado **antes** de `TryMarkFailedAsync(id, "MafAgent.PendingAction.UnexpectedFailure", now)`, resultado `Status=Failed`, sin `AppendSystemNoteAsync`, y registro en el log; `ResultJson` guardado cifrado (`IEncryptionService.Encrypt` invocado con el resultado); una acción sigue confirmable después de que se proponga otra en la misma conversación; nota de resultado añadida a la sesión | T014, T016, T018 | AC3, AC5, AC6, AC7, AC10, AC12, AC16, AC18 |
| T020 |  | Implement `ConfirmPendingActionCommand` (+ validator, `ICommand`) | T019 verde | T019 | igual que T019 |
| T021 | [P] | Write failing tests for `RejectPendingActionCommandHandler` | `dotnet test tests/Application.Tests --filter "FullyQualifiedName~RejectPendingActionCommandHandlerTests"` rojo: `Pending` → `Rejected` **mediante `TryRejectAsync`**, nunca con un UPDATE de la entidad cargada; `TryRejectAsync=false` → `AlreadyDecided`; de otro usuario → `NotFound`; expirada → `Expired`; ya decidida → `AlreadyDecided`; 0 despachos a `ISender` | T008 | spec Behaviour §Escritura 5, AC6, AC7 |
| T022 |  | Implement `RejectPendingActionCommand` | T021 verde | T021 | igual que T021 |
| T023 |  | Write failing tests for `SendMafAgentMessageCommandHandler` | `dotnet test tests/Application.Tests --filter "FullyQualifiedName~SendMafAgentMessageCommandHandlerTests"` rojo: `ConversationId` null → crea conversación; de otro usuario → `Conversation.NotFound`; más de 24 h → `Conversation.Expired` sin invocar al runner; sesión cifrada al guardar y descifrada al cargar; `LastActivityAt` actualizado; los `PendingActionDto` de la respuesta se construyen desde `MafAgentTurnResult.ProposedActions` (instancias preparadas, sin releer de BD) y contienen solo las del turno; el runner lanza `AgentLoopExhaustedException`/`AgentTimeoutException` → `MafAgent.LoopExhausted`/`MafAgent.Timeout`; ningún camino invoca `PendingActionExecutor` | T008, T014 | AC4, AC13, AC14, AC15, AC16 |
| T024 |  | Implement `SendMafAgentMessageCommand` (+ validator 1..2000, `ICommand`) and register Application MafAgent services | T023 verde; los validadores nuevos son `public sealed`; `dotnet test tests/Application.Tests --filter "FullyQualifiedName~MafAgentApplicationDiTests"` verde: un `ServiceCollection` con `services.AddApplication(new ConfigurationManager())` (firma real `AddApplication(IServiceCollection, ConfigurationManager)`), más dobles NSubstitute registrados para los puertos de otras capas (`IEncryptionService`, `ICurrentUserProvider`, `IAddedEntityIdLog`, `IPendingActionRepository`, `IMafConversationRepository`, `ITrackedChangesDiscarder`, `IMafAgentRunner`), resuelve `PendingActionFactory`, `ICurrentValuesReader` y `PendingActionExecutor`, e `IValidator<SendMafAgentMessageCommand>` rechaza un mensaje de 2001 caracteres (analyze M2) | T023 | igual que T023 |
| T025 |  | Write failing tests for MafAgent read tools | `dotnet test tests/WebApi.MinimalAPI.Tests --filter "FullyQualifiedName~MafAgentReadToolsTests"` rojo: las 4 herramientas heredadas (`ListMyPortfolios`, `GetPortfolioValue`, `GetPortfolioHoldings`, `GetAssetPeakOrTroughDate`) devuelven los mismos datos que sus homólogos de `PortfolioAgentPlugin` en `development` con el mismo seed; no existe ninguna herramienta de comparación entre fechas; entries/history/allocation/catálogos/notas devuelven el seed; portafolio de otro usuario → NotFound; notas visibles para cualquier usuario | T001, T009 | AC1, AC1b |
| T026 |  | Implement MafAgent read tools | T025 verde | T025 | AC1, AC1b |
| T027 |  | Write failing tests for MafAgent propose tools | `dotnet test tests/WebApi.MinimalAPI.Tests --filter "FullyQualifiedName~MafAgentProposeToolsTests"` rojo: datos completos → 1 `PendingAction` preparada y 0 mutaciones de negocio; dato que falta o ambiguo → 0 acciones y el campo señalado; símbolo o exchange desconocido → 0 acciones; 3 propuestas en un turno → 3 acciones independientes; ningún parámetro `userId` (por reflexión); `ProposeUpdate*` lee el estado actual con `ICurrentValuesReader`, fusiona solo los campos pedidos y guarda la petición completa (p. ej. cambiar solo `quantity` conserva `PricePerUnit`, `RecordedAt` y los ids; editar el título de una nota inactiva conserva `IsActive=false`); `ProposeUpdate*` sin cambios reales → `MissingOrAmbiguous:changes`; `symbol`/`exchangeName` se resuelven a id sin distinguir mayúsculas, y 0 coincidencias → `UnknownCatalogItem`; `ProposeSendStatusReport` sin periodo guarda las fechas concretas de `PreviousCalendarMonth` calculadas al proponer; las fechas recibidas como `DateTimeOffset` con `Z`, con `+01:00` y sin offset (interpretada en `Europe/Madrid`) acaban todas en el payload con `Kind=Utc` y el mismo instante; `Summary` no contiene el título ni el contenido de la nota | T014, T016, T026 | AC2, AC9, AC17, AC19 |
| T028 |  | Implement MafAgent propose tools | T027 verde | T027 | AC2, AC9, AC17, AC19 |
| T029 |  | Write failing tests for `MafAgentRunner` | `dotnet test tests/WebApi.MinimalAPI.Tests --filter "FullyQualifiedName~MafAgentRunnerTests"` rojo, con un `IChatClient` falso: el conjunto de herramientas no contiene ninguna de borrado ni de escritura de `CryptoCurrency`/`Exchange`; 6 llamadas a herramientas con límite 5 → `AgentLoopExhaustedException`, lanzada por la middleware contadora de plan §0 "Límite de iteraciones" (el test falla si solo se configura `MaximumIterationsPerRequest`); timeout de inferencia → `AgentTimeoutException`; la sesión serializada se restaura con sus turnos previos; se inyectan en el contexto la hora UTC actual, la hora local de `Europe/Madrid` con su offset y el id de zona (con `FakeTimeProvider` en un instante como `2026-11-10T23:30:00Z`, donde el día local ya es el 11); el log de herramientas no contiene argumentos (sink en memoria); `AiOptions.Provider=Ollama` → cliente OpenAI-compatible con `Endpoint`, `Provider=AzureOpenAI` → cliente Azure con `DeploymentName`, con las mismas herramientas e instrucciones (sin red); las instrucciones contienen las 4 reglas de plan §0 (idioma, no soportado, dato faltante, fechas en `Europe/Madrid`) | T001, T028 | AC8, AC9, AC14, AC15, AC19, spec Non-goals (proveedor), spec Behaviour (idioma), plan §0 Logs |
| T030 |  | Implement `MafAgentRunner`, chat-client/agent factory from `AiOptions`, DI wiring | T029 verde; `UNIT` verde | T029 | AC8, AC14, AC15 |
| T031 |  | Write failing HTTP tests for MafAgent endpoints | `dotnet test tests/WebApi.MinimalAPI.Tests --filter "FullyQualifiedName~MafAgentEndpointsTests"` rojo, con `MafAgentApiFactory` (T002): `chat` 200 con `conversationId` y `pendingActions`; un chat con el texto "sí", "confirmo" o "<id>" no ejecuta nada; `confirm` devuelve 200/404/409; confirmar la acción de otro usuario da el mismo 404 que un id inexistente; conversación ajena → 404; sin JWT → 401; confirmar una `CreateEntry` devuelve `200` con `Status=Succeeded` | T002, T009, T012, T020, T022, T024, T030, T038 | AC4, AC7, AC13 |
| T032 |  | Implement `MafAgentEndpoints` + `EndpointRegistrar` + `EndpointTags` | T031 verde; `grep -n "MafAgent" src/WebApi.MinimalAPI/Program.cs` sin resultados | T031 | spec Behaviour, plan §3 |
| T033 |  | Write and pass restart integration test | `INTEG` con `--filter "FullyQualifiedName~MafAgentRestartTests"` verde: conversación y acción persistidas en Postgres; un segundo `ServiceProvider` continúa la conversación y confirma la acción; confirmar una `CreateEntry` contra Postgres devuelve un id > 0 igual al de la fila insertada | T012, T030, T032, T038 | AC3, AC15 |
| T039 |  | Write SK agent regression smoke tests (baseline before MAF) | antes de añadir ningún paquete MAF: `dotnet test tests/WebApi.MinimalAPI.Tests --filter "FullyQualifiedName~SkAgentRegressionTests"` **verde** sobre `development`: `KernelFactory.CreateKernel()` construye un kernel con el plugin `PortfolioOperations` (4 funciones, mismos nombres) y los filtros `LoopCappingFilter`/`ToolInvocationAuditFilter`; las 4 funciones de `PortfolioAgentPlugin` devuelven los datos del seed; sin red (analyze M7). Solo añade tests: no toca código de `*/Agent/` | — | AC11 |
| T040 |  | Write failing tests for `LoggingBehavior` redaction | `dotnet test tests/Application.Tests --filter "FullyQualifiedName~LoggingBehaviorRedactionTests"` rojo: con un logger en memoria, las propiedades `Message`, `Reply`, `Values`, `Before`, `Result`, `Content` y `Summary` no aparecen en el log de petición ni de respuesta (incluido dentro de colecciones anidadas, como `PendingActions[]`), y las demás propiedades sí | — | spec Behaviour §Conversación (protección en reposo), plan §0 Logs |
| T041 |  | Extend `LoggingBehavior.SensitivePropertyNames` | T040 verde; `UNIT` verde | T040 | igual que T040 |
| T037 |  | Write failing tests for `AddedEntityIdLog` interceptor | `dotnet test tests/WebApi.MinimalAPI.Tests --filter "FullyQualifiedName~AddedEntityIdLogTests"` rojo: tras un `SaveChanges` que inserta un `PortfolioEntry` (EF InMemory), `LastAddedId<PortfolioEntry>()` devuelve el id asignado (> 0); sin inserciones → `null`; 2 inserciones del mismo tipo → la última; tipos distintos no se mezclan | T002, T009 | AC3, AC12 |
| T038 |  | Implement `AddedEntityIdLog` (`SaveChangesInterceptor` scoped) and register it on `AppDbContext` | T037 verde; `UNIT` verde (los tests existentes no cambian de comportamiento) | T037 | AC3, AC12 |
| T034 |  | Run regression of existing suite and SK agent | `UNIT` y `INTEG` verdes; `git diff --stat development -- src/Infrastructure/Agent src/Application/Agent src/WebApi.MinimalAPI/Endpoints/Agent` vacío; `git diff development -- src/Infrastructure/Infrastructure.csproj \| grep -E '^[+-].*SemanticKernel'` vacío (solo líneas añadidas o quitadas, no las de contexto; analyze ejecución 4, M9) (salvo bump aprobado y registrado en `decisions.md`); `SkAgentRegressionTests` (T039) verdes; las versiones de `Microsoft.Extensions.AI*` coinciden con las de `docs/maf-agent-packages.md` aprobadas en T001 | T033, T039 | AC11 |
| T035 |  | Document MafAgent flow + manual model checklist | `docs/maf-agent-flow.md` creado (flujos A y B, errores, configuración, decisiones); lista de comprobación manual ejecutada con Ollama: las 4 preguntas de AC1; "registra 0.5 BTC a 60000 en Binance ayer" (propone con fecha ISO Z); "borra el portafolio X" (explica que no está soportado, sin propuesta); "registra una compra de BTC" sin precio (pide el precio, sin propuesta); una pregunta en inglés (responde en inglés). Cada caso queda anotado en el doc con OK o FALLO | T034 | AC1, AC8, AC9, AC19, spec Behaviour (idioma), área no formulable |
| T036 |  | All done-checks pass + verify gate | cada fila marcada con su evidencia; `dotnet format portfolio-management.slnx --verify-no-changes` exit 0; `dotnet build` exit 0; `UNIT` y `INTEG` verdes | all | spec §Acceptance |

### Interfaces por tarea

**T002 — Interfaces**
- Produces: `MafAgentApiFactory : WebApplicationFactory<Program>` con `CreateClientForUser(long userId) -> HttpClient`, `FakeTimeProvider Time { get; }`, `IMafAgentRunner Runner { get; }` (NSubstitute) y un `AppDbContext` InMemory compartido por fixture.

**T006 — Interfaces**
- Produces: `enum PendingActionOperation { CreatePortfolio, UpdatePortfolio, CreateEntry, UpdateEntry, CreateNote, UpdateNote, RefreshPrices, SendStatusReport }`; `enum PendingActionStatus { Pending, Executing, Succeeded, Failed, Rejected, Stale }`; `PendingAction.Create(Guid conversationId, long userId, PendingActionOperation op, string payloadJson, string? beforeJson, DateTime nowUtc)`; `MarkExecuting()`, `MarkSucceeded(string resultJson, DateTime nowUtc)`, `MarkFailed(string errorCode, DateTime nowUtc)`, `MarkRejected(DateTime nowUtc)`, `MarkStale(DateTime nowUtc)`; `bool IsExpired(DateTime nowUtc)`. Las entidades son `public sealed`.

**T008 — Interfaces**
- Produces:
  - `IMafConversationRepository { Task<MafConversation?> GetByIdAsync(Guid id, CancellationToken ct); void Add(MafConversation c); void Update(MafConversation c); }`
  - `IPendingActionRepository { Task<PendingAction?> GetByIdAsync(Guid id, CancellationToken ct); void Add(PendingAction a); void Update(PendingAction a); Task<bool> TryClaimAsync(Guid id, long userId, DateTime nowUtc, CancellationToken ct); Task<bool> TryRejectAsync(Guid id, long userId, DateTime nowUtc, CancellationToken ct); Task<bool> TryMarkFailedAsync(Guid id, string errorCode, DateTime nowUtc, CancellationToken ct); }`
  - `ITrackedChangesDiscarder { void DiscardAll(); }`
  - `IAddedEntityIdLog { long? LastAddedId<TEntity>() where TEntity : Entity; }`
  - `ICurrentValuesReader { Task<ErrorOr<IReadOnlyDictionary<string, string?>>> ReadAsync(PendingActionOperation op, string targetId, CancellationToken ct); }`
  - `IMafAgentRunner { Task<MafAgentTurnResult> RunTurnAsync(MafConversation conversation, string decryptedSessionJson, string message, CancellationToken ct); Task<string> AppendSystemNoteAsync(string decryptedSessionJson, string note, CancellationToken ct); }`
  - `sealed record MafAgentTurnResult(string Reply, string SerializedSessionJson, IReadOnlyList<PendingAction> ProposedActions);` (las mismas instancias preparadas con `Add`, todavía sin commit)

**T014 — Interfaces**
- Consumes: `IEncryptionService.Encrypt/Decrypt(string)`, `TimeProvider`, `ICurrentUserProvider.UserId`.
- Produces: `PendingActionFactory.Create(Guid conversationId, PendingActionOperation op, object payload, object? before) -> PendingAction` y `PendingActionFactory.ToDto(PendingAction a) -> PendingActionDto` (descifra; fechas en formato `"yyyy-MM-ddTHH:mm:ssZ"`).

**T018 — Interfaces**
- Consumes (firmas exactas existentes): `CreatePortfolioCommand(CreatePortfolioRequest)`, `UpdatePortfolioCommand(long Id, UpdatePortfolioRequest)`, `CreatePortfolioEntryCommand(CreatePortfolioEntryRequest)`, `UpdatePortfolioEntryCommand(long Id, UpdatePortfolioEntryRequest)`, `CreateNoteCommand(CreateNoteRequest)`, `UpdateNoteCommand(string Id, UpdateNoteRequest)`, `RefreshPortfolioPricesCommand(long Id)`, `TriggerPortfolioStatusReportCommand(long PortfolioId, DateTime? PeriodStart, DateTime? PeriodEnd)`.
- Consumes también: `IAddedEntityIdLog.LastAddedId<Portfolio>()` / `LastAddedId<PortfolioEntry>()` (id real tras el commit); payloads `Update*` = peticiones completas (`UpdatePortfolioRequest`, `UpdatePortfolioEntryRequest`, `UpdateNoteRequest` incl. `IsActive`).
- Produces: `PendingActionExecutor.ExecuteAsync(PendingAction action, CancellationToken ct) -> Task<ErrorOr<object>>`. Quien lo llama pasa `CancellationToken.None`.

**T016 — Interfaces**
- Consumes: `ISender`; `GetPortfolioByIdQuery`, `GetPortfolioEntryByIdQuery`, `GetNoteByIdQuery` (queries existentes).
- Produces: `ICurrentValuesReader` (T008) y `static bool StaleCheck.IsStale(IReadOnlyDictionary<string, string?> before, IReadOnlyDictionary<string, string?> current)`. Formato de los valores del snapshot: decimales `ToString("G29", CultureInfo.InvariantCulture)`, fechas `ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)` (precisión completa) y booleanos `"true"`/`"false"`. `yyyy-MM-ddTHH:mm:ssZ` es solo de presentación (plan §0 "Payloads tipados"). `StaleCheck` y `CurrentValuesReader` son `public` (la herramienta de Infrastructure los consume).

**T020 — Interfaces**
- Consumes: `IPendingActionRepository` (incl. `TryClaimAsync` y `TryMarkFailedAsync`), `ITrackedChangesDiscarder.DiscardAll()`, `IMafConversationRepository`, `ICurrentValuesReader`, `StaleCheck.IsStale`, `PendingActionExecutor.ExecuteAsync`, `IMafAgentRunner.AppendSystemNoteAsync`, `IEncryptionService`, `ICurrentUserProvider`, `TimeProvider`.
- Produces: `ConfirmPendingActionCommand(Guid Id) : IRequest<ErrorOr<PendingActionResultDto>>, ICommand`. Errores (solo antes del reclamo): `MafAgent.PendingAction.NotFound`, `.Expired`, `.AlreadyDecided`. Después del reclamo siempre devuelve un valor con `Status ∈ {Succeeded, Failed, Stale}`. Es el único punto del código con `catch (Exception)`, alrededor de `ExecuteAsync` (plan §0 "Tras el reclamo"), con `ErrorCode = "MafAgent.PendingAction.UnexpectedFailure"`.

**T024 — Interfaces**
- Produces: `SendMafAgentMessageCommand(SendMafAgentMessageRequest Request) : IRequest<ErrorOr<SendMafAgentMessageResponse>>, ICommand`.

**T026 / T028 — Interfaces**
- Consumes: `ISender` (queries existentes), `IPortfolioRepository`, `IPortfolioEntryRepository`, `INoteRepository`, `ICurrentUserProvider`, `PendingActionFactory`, `IPendingActionRepository.Add`.
- Produces: `MafAgentTools.GetTools() -> IReadOnlyList<AIFunction>` con los nombres exactos de plan §3 (sin sufijo `Async`). Cada `Propose*` devuelve al modelo `PendingActionSummary { Id, Operation, Values, Before, ExpiresAt }`, o un error textual `MissingOrAmbiguous:<campo>` / `NotFound` / `UnknownCatalogItem:<valor>`. Además expone `MafAgentTools.ProposedActions -> IReadOnlyList<PendingAction>` (una instancia por turno), y cada `ProposeUpdate*` usa `ICurrentValuesReader` para construir `Before`.

**T030 — Interfaces**
- Consumes: `AiOptions` (`Provider`, `ReasoningModel`, `MaxAllowedToolLoops`, `RequestTimeoutSeconds`), `IHttpClientFactory.CreateClient("InferenceClient")`, `MafAgentTools` y las versiones fijadas en T001.
- Produces: la implementación de `IMafAgentRunner` (T008), registrada como scoped, con la middleware contadora de plan §0 "Límite de iteraciones" como mecanismo único de `AgentLoopExhaustedException`.

## Review Workload Forecast

| Dimension | Forecast | Why |
| --- | --- | --- |
| Estimated changed lines | 2 800 – 3 800 (≈45 % tests) | 36 tareas, 8 operaciones con payload, unas 19 herramientas, migración y 3 endpoints |
| Files / areas | unos 45 archivos en 6 proyectos: Domain, Contracts, Application/MafAgent, Infrastructure/MafAgent + persistencia + migración, WebApi/Endpoints/MafAgent, 3 proyectos de test y docs | feature transversal a todas las capas |
| Review risk | high | escrituras sobre datos reales, garantía de concurrencia, cifrado, una librería joven (MAF) y convivencia de paquetes con SK |
| Suggested delivery | autochain (PRs encadenados sobre `feature/maf-tool-calling-agent`) | supera con mucho el presupuesto por defecto de 400 líneas. Cortes naturales: **PR1** T039, T040, T041 y T001 (red de seguridad SK, redacción de logs, spike y versiones) · **PR2** T002–T012 + T037/T038 (dominio, contratos, puertos, persistencia, claim atómico) · **PR3** T013–T024 (Application: propuesta, desfase, ejecutor, confirmar/rechazar/chat) · **PR4** T025–T030 (herramientas y runner MAF) · **PR5** T031–T036 (HTTP, integración, regresión, docs) |
