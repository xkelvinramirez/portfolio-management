---
type: analysis
title: Analysis — Agente MAF con tool-calling
description: Puerta de consistencia pre-implementación (ejecución 4) que cruza la constitución, la spec, el plan y las tareas de maf-tool-calling-agent.
tags: [sdd, analysis, gate]
timestamp: 2026-11-11T22:00:00Z
topic: sdd
slug: maf-tool-calling-agent
---

# Analysis — Agente MAF con tool-calling (ejecución 4)

> Artefactos: [constitución](../constitution.md) (borrador; canon = `CLAUDE.md`) · [spec](../specs/maf-tool-calling-agent.md) (clarified; AC1 = 4 preguntas) · [plan + tareas](../plans/maf-tool-calling-agent.md) (T001–T041, revisado ×3)
> Método: **revisor independiente nuevo**, sin acceso a informes anteriores, contrastado con `src/` y `tests/`, con severidad calibrada de forma estricta. El HIGH que propuso se verificó en el código antes de clasificarlo.

## Veredicto

**GATE: PASS** — CRITICAL 0 · HIGH 0 · **MEDIUM 10** · LOW 3.

- **Las correcciones de las ejecuciones 1 a 3 se sostienen.** El revisor confirmó contra el código las firmas de los comandos y queries, los campos de las peticiones (incl. `IsActive` y los ids de catálogo), `PreviousCalendarMonth`, `Updated`/`Skipped`, `AiOptions`, las excepciones de Domain, la visibilidad pública del agente SK (T039 es viable), el `AddDbContext` sin pooling (el interceptor scoped funciona), `timestamptz`, que `ValidationBehavior` no lanza, y que los handlers no preparan cambios antes de devolver un error.
- **Un HIGH propuesto se rebajó a MEDIUM (M1)** tras verificarlo. Esa decisión determina el veredicto, así que se explica abajo. Si el usuario no la comparte, el veredicto pasa a BLOCKED.
- La tendencia confirma la lectura de la ejecución 3: los hallazgos ya son de **robustez y de precisión en los contratos**, no de diseño. Son el tipo de defecto que TDD y `review` atrapan bien.

## Mapa de cobertura

Sin cambios respecto a la ejecución 3, salvo:

```text
REQ    | Requisito (spec)                         | Plan / Tareas                  | Estado
------ | ---------------------------------------- | ------------------------------ | ----------
AC1    | 4 preguntas (comparación diferida)        | §3 Paridad, T025, T039         | covered
AC12   | Auditoría de desenlace                    | §0 Tras el reclamo, T019       | covered, salvo cancelación → M1
AC3    | Exactamente lo mostrado (fechas)          | §0 Payloads tipados, T027      | AMBIGUOUS → M2 (DateTime.Kind)
AC14   | Límite de pasos → error                   | T029/T030                      | AMBIGUOUS → M10 (mecanismo)
B-pers | Nada sensible en logs                     | §0 Logs MediatR, T040          | AMBIGUOUS → M3 (Summary)
```

No hay GAP ni DRIFT.

## Hallazgos

| # | Sev. | Tipo | Artefacto A (loc) | Artefacto B (loc) | Conflicto | Resolver en |
| --- | --- | --- | --- | --- | --- | --- |
| M1 | MEDIUM *(propuesto HIGH)* | Robustez | plan §0 "Tras el reclamo" (2) (`CancellationToken.None` solo para el ejecutor) | `UnitOfWorkBehavior.cs:17-21` (commit externo con el token de la petición) | Si la petición se cancela después del commit interno, el commit externo lanza `OperationCanceledException` y la acción queda en `Executing` sin desenlace, aunque el cambio de negocio sí ocurrió. **Por qué no es HIGH:** ningún endpoint del repo pasa `CancellationToken` a `ISender.Send` (0 de 7), así que el token efectivo ya es `None` si T032 sigue la convención. Es latente, no está garantizado. *Arreglo:* fijar en §0 que `MafAgentEndpoints` no pasa token a `Send` para confirm, o lo pasa como `None`. | plan §0 + T019/T032 |
| M2 | MEDIUM | Datos | plan §0 "Payloads tipados" (supone `Kind=Utc`); T027/T028 | `PortfolioEntryRepository.cs:34-39` (Npgsql rechaza `Unspecified`) | Si el modelo envía una fecha sin `Z` u otro offset, el `Kind` no es UTC: `Values` mostraría otro instante y el commit fallaría (`Failed`), sin datos erróneos. *Arreglo:* parámetros `DateTimeOffset` → `.UtcDateTime`, y un caso en T027. | plan §0 + T027 |
| M3 | MEDIUM | Privacidad | plan §0 "Logs de MediatR" (lista de redacción) | `PendingActionDto.Summary` (contenido no especificado) | `Summary` no está en la lista. Si repite valores (p. ej. el texto de una nota), llega a Seq. *Arreglo:* añadir `Summary` a la lista o fijar que no repite valores, y comprobarlo en T040. | plan §0 + T040 |
| M4 | MEDIUM | Orden | T031 (Depends-on) | T012 (`ITrackedChangesDiscarder` registrado) | Confirm necesita `ITrackedChangesDiscarder`, pero T012 no está aguas arriba de T031. | tasks |
| M5 | MEDIUM | Done-check | T024 (`AddApplication()`) | `Application/DependencyInjection.cs:10-11` (`AddApplication(IServiceCollection, ConfigurationManager)`) | La firma literal no existe, y las dependencias (`IEncryptionService`, `ICurrentUserProvider`, `IAddedEntityIdLog`) son de otras capas. *Arreglo:* el test pasa un `ConfigurationManager` y dobles para esos puertos. | tasks |
| M6 | MEDIUM | Carrier | T004 (sin Interfaces); §4 A.4 vs Interfaces de T014/T026/T028 | — | Faltan la factoría de `MafConversation`, cómo se actualiza la sesión cifrada, el JSON inicial de una conversación nueva y cómo conocen las herramientas el `conversationId`. Además, quién llama a `IPendingActionRepository.Add` es contradictorio: según §4 A.4 la factoría, según T026/T028 las herramientas. | plan §3/§4 + tasks |
| M7 | MEDIUM | Concurrencia | plan §4 B.6 (confirm reescribe la sesión) | turno de chat simultáneo sobre la misma conversación | Escritura perdida en `EncryptedSessionState`: la nota de resultado puede desaparecer. Con un solo usuario, el impacto es bajo. *Arreglo:* token `xmin`, o aceptarlo y documentarlo. | plan §7 (aceptar) |
| M8 | MEDIUM | Harness | T011, T033; §5 ("Postgres real") | `WebApi.MinimalAPI.Tests` sin `UserSecretsId`; la BD de desarrollo es compartida | No se dice de dónde sacan los tests `INTEG` la cadena de conexión, cómo siembran las FK (`User`, `MafConversation`) ni cómo limpian. T033 necesita un host con Postgres que ninguna tarea construye. | tasks (T011/T033) |
| M9 | MEDIUM | Done-check | T034 (`git diff … \| grep -i "SemanticKernel"`) | `Infrastructure.csproj:28-30` (líneas de contexto del diff) | El grep también encaja con líneas de contexto sin cambios: daría un falso fallo. *Arreglo:* `grep -E '^[+-].*SemanticKernel'`. | tasks |
| M10 | MEDIUM | Carrier | T029 ("6 llamadas → excepción"); T030 Interfaces | comportamiento de `FunctionInvokingChatClient.MaximumIterationsPerRequest` (el revisor cree que corta sin lanzar; **no verificado** en la fuente de MEAI) | No se especifica qué produce `AgentLoopExhaustedException`. Configurar solo `MaximumIterationsPerRequest` probablemente no cumpla T029. *Arreglo:* una middleware de función que cuente y lance. | plan §3 + T030 |
| L1 | LOW | Wording | plan §2 (`CurrentValuesReader` `internal sealed`) | Interfaces de T016 ("son `public`") | Contradicción menor: la implementación puede ser internal, siempre que el registro DI lo permita. | plan |
| L2 | LOW | Carrier | T002 (repo de test: `TryClaimAsync`/`TryRejectAsync`) | T008 (`TryMarkFailedAsync`) | El doble debe implementar también `TryMarkFailedAsync` para compilar. | tasks |
| L3 | LOW | Harness | T002 (cambia `DbContextOptions` a InMemory) | T038 (interceptor registrado en `AppDbContext`) | Si la factoría reemplaza las opciones sin volver a añadir el interceptor, `IAddedEntityIdLog` devuelve `null` en T031. | tasks |
| — | — | Arrastre | constitución en borrador; `config.yaml` ausente | — | G9/G11: siguen abiertos, no bloquean. | constitution / sdd-init |

## Recomendación

La puerta **está abierta**. Los MEDIUM restantes se pueden **aceptar de forma consciente** o **arreglar antes de `implement`**. Arreglarlos es barato: casi todos son una línea en §0 o en una fila de tareas.

- **Merece la pena arreglarlos ahora (≈1 pasada corta de `tasks`, sin `analyze` de nuevo):** M1, M2, M3, M4, M5, M9, M10, L2, L3. Si no, cada uno aparecería como un fallo o un re-trabajo durante `implement`.
- **Aceptables o resolubles por el implementador:** M6 (los contratos los fija la primera tarea que los produce), M7 (riesgo aceptado con un solo usuario), M8 (lo concreta T011 al crear el primer test de integración), L1.

El usuario decide.
