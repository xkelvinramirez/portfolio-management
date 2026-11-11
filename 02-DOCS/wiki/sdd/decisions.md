---
type: decisions
title: SDD decisions log
description: Append-only log of scoping and design decisions taken across the SDD chain.
tags: [sdd, decisions]
timestamp: 2026-11-11T12:00:00Z
topic: sdd
---

# SDD decisions log (append-only)

| Date | Feature | Decision | Why | Phase |
|------|---------|----------|-----|-------|
| 2026-11-11 | maf-tool-calling-agent | Crear un agente NUEVO con MAF en lugar de migrar o ampliar el agente de Semantic Kernel. | Será el relevo futuro de SK. Conviven y la retirada de SK queda diferida. Se registró la objeción de duplicación y se aceptó como temporal. | specify |
| 2026-11-11 | maf-tool-calling-agent | Alcance: lectura completa más escritura (crear/editar) de portafolios, entradas y notas, y acciones de sistema (refrescar precios, informe de estado). Sin borrado y sin escritura en catálogos en v1. | Acercar "operaciones de la app" sin operaciones irreversibles ni datos compartidos. | specify |
| 2026-11-11 | maf-tool-calling-agent | Toda escritura pasa por una "acción pendiente" con id, que el usuario confirma explícitamente. Un "sí" en texto libre no vale. | La garantía no puede depender de cómo interprete un modelo local pequeño. | specify |
| 2026-11-11 | maf-tool-calling-agent | Solo API en v1. El frontend se difiere. | Acotar el ciclo. | specify |
| 2026-11-11 | maf-tool-calling-agent | Seguir con la constitución en borrador, usando CLAUDE.md como canon de facto (anotado como suposición). | Decisión del usuario para no bloquear la spec. | specify |
| 2026-11-11 | maf-tool-calling-agent | Conversación persistida, sobrevive a reinicios, caduca a las 24 h sin actividad; protegida en reposo como las notas. | Un redeploy no debe perder acciones pendientes, y la conversación puede contener texto de notas descifrado. | clarify |
| 2026-11-11 | maf-tool-calling-agent | El agente lee y escribe el contenido de las notas con Ollama local y con Azure OpenAI. | Editar una nota sin ver su contenido no tiene sentido. | clarify |
| 2026-11-11 | maf-tool-calling-agent | Una acción pendiente por escritura. Se rechaza confirmar una edición si el recurso cambió después de la propuesta. | Sin transacciones multi-SaveChanges. Nunca se pisa en silencio un cambio no visto. | clarify |
| 2026-11-11 | maf-tool-calling-agent | Las notas no tienen dueño: el agente las trata con la misma visibilidad que la API de notas. La spec se enmienda. | Contradicción entre spec y código hallada en plan. Decisión del usuario. Sin migración. | plan |
| 2026-11-11 | maf-tool-calling-agent | Las acciones pendientes son entidades de la app (Postgres), no aprobaciones de MAF (`ApprovalRequiredAIFunction`). Las herramientas solo proponen y la ejecución va por un endpoint de confirmación sin modelo. | AC4/AC5/AC6/AC7/AC18 exigen garantías consultables y deterministas. MAF reanuda el modelo al aprobar y guarda las aprobaciones dentro de la sesión. | plan |
| 2026-11-11 | maf-tool-calling-agent | "Como mucho una vez" vía UPDATE condicional atómico (`TryClaimAsync`), única excepción al patrón UoW. La confirmación ejecuta los comandos existentes por ISender. | Garantía de AC5 en la base de datos. Se reutiliza la validación, la comprobación de dueño, la caché y el commit de cada feature. | plan |
| 2026-11-11 | maf-tool-calling-agent | `PendingAction` es el registro de auditoría (AC12). Sin tabla de auditoría aparte. Payloads y sesión cifrados. | Evitar duplicar datos. Los payloads pueden contener texto de notas. | plan |
| 2026-11-11 | maf-tool-calling-agent | Tras `TryClaimAsync=true`, confirmar nunca devuelve error. `Stale`/`Failed`/`Succeeded` van como 200 con `Status`. `ResultJson` se cifra. | analyze F1 (la UoW no persistía `Stale`) y F2 (contenido de notas en claro). | plan (post-analyze) |
| 2026-11-11 | maf-tool-calling-agent | `POST chat` sin `Idempotency-Key`: desviación aceptada de `CLAUDE.md`. | analyze F5. Los duplicados por reintento son inocuos: requieren confirmación y caducan. Decisión del usuario. | plan (post-analyze) |
| 2026-11-11 | maf-tool-calling-agent | `Microsoft.SemanticKernel` 1.81.0 no se cambia sin aprobación explícita del usuario. | analyze F8: un bump modifica el agente actual (spec Non-goals). | plan (post-analyze) |
| 2026-11-11 | maf-tool-calling-agent | Ediciones = petición completa fusionada con el estado actual; Before cubre todos los campos. | analyze G1: los comandos de edición reemplazan todos los campos (incl. `IsActive` de las notas). | plan (post-analyze 2) |
| 2026-11-11 | maf-tool-calling-agent | Id real de las creaciones vía `IAddedEntityIdLog` (SaveChangesInterceptor). Los POST existentes que devuelven id 0 quedan fuera de alcance (posible FTD aparte). | analyze G2: la respuesta se construye antes del commit. | plan (post-analyze 2) |
| 2026-11-11 | maf-tool-calling-agent | Zona horaria de negocio `Europe/Madrid` inyectada en el contexto del agente. `Summary` muestra la hora local. | analyze G6, decisión del usuario. | plan (post-analyze 2) |
| 2026-11-11 | maf-tool-calling-agent | Desviación de `CLAUDE.md`: un único `catch (Exception)` en `ConfirmPendingActionCommandHandler` tras el reclamo, para persistir `Failed`. Reject atómico con `TryRejectAsync`. | analyze G8 y G4. Después de un reclamo irreversible, el desenlace debe quedar registrado. Pendiente de ratificar con la constitución. | plan (post-analyze 2) |
| 2026-11-11 | maf-tool-calling-agent | AC1 = 4 preguntas. La comparación entre fechas queda diferida hasta que su rama llegue a `development`. | analyze H1: premisa falsa sobre la rama base. Decisión del usuario (opción a). | clarify (post-analyze 3) |
| 2026-11-11 | maf-tool-calling-agent | Rama de excepción en confirm: `DiscardAll()` del tracker + `TryMarkFailedAsync` atómico. Nueva excepción a la UoW. | analyze H3: evitar guardar un cambio de negocio informando `Failed`. | plan (post-analyze 3) |
| 2026-11-11 | maf-tool-calling-agent | `LoggingBehavior` redacta `Message`, `Reply`, `Values`, `Before`, `Result` y `Content` (cambio transversal). | analyze M4: texto del chat y contenido de notas en Seq. | plan (post-analyze 3) |
| 2026-11-11 | maf-tool-calling-agent | Tests de regresión del agente SK antes de añadir MAF (T039). Vigilancia de versiones transitivas de `Microsoft.Extensions.AI*`. | analyze M7: AC11 no tenía red de seguridad. | plan (post-analyze 3) |
| 2026-11-11 | maf-tool-calling-agent | Tras analyze ejecución 4 (GATE: PASS): se corrigen M1, M2, M3, M4, M5, M9, M10, L2 y L3 en plan/tareas. Se **aceptan** M6 (contratos de MafConversation), M7 (escritura perdida de la sesión con un solo usuario), M8 (harness de integración con Postgres) y L1. Los fija la tarea productora y los comprueba `review`. | Decisión del usuario. El análisis en papel daba ya rendimientos decrecientes. | tasks (post-analyze 4) |
| 2026-11-11 | (infra) | Añadir `global.json` con `"test": { "runner": "Microsoft.Testing.Platform" }`. Comandos de test con sintaxis MTP (`--filter-trait`/`--filter-not-trait`/`--filter-class`) en `config.yaml`. Commit aparte en `feature/maf-tool-calling-agent`, para llevarlo luego a `development`. | `dotnet test` roto desde 7190d30 (xunit.v3 sin opt-in de MTP en el SDK 10.0.403). Verificado: 145/145 verdes. Decisión del usuario. | sdd-init |
