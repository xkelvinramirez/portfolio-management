---
type: verification
title: Verification — maf-tool-calling-agent — 2026-11-11 — T009–T012 (pre-commit)
description: Verificación de alcance de tarea antes del commit de persistencia, migración y reclamo atómico (PR2). No es el veredicto de la feature.
tags: [sdd, verification]
timestamp: 2026-11-12T01:30:00Z
topic: sdd
slug: maf-tool-calling-agent
source_state: c3e4c05 on feature/maf-tool-calling-agent-pr2 (dirty — the T009–T012 changes under verification)
---

# Verification — maf-tool-calling-agent — 2026-11-11 — T009–T012

## Stack gate (config.yaml; una ejecución tras la última edición)
- build → PASS (0 avisos, 0 errores)
- format `--verify-no-changes` → PASS (exit 0)
- unit → PASS — 188/188 (186 + 2 de `TrackedChangesDiscarderTests`)
- integration → PASS — 9/9 `PendingActionRepositoryClaimTests` contra el Postgres de desarrollo (Supabase, confirmado por el usuario como solo de desarrollo)

## Task done-checks
- [x] T009 — `MafConversationConfiguration`/`PendingActionConfiguration` (`ToTable("mafagent_*")`, columnas snake_case, enums como texto, índices `user_id` y `conversation_id`, FKs a `Users` y a la conversación); `DbSet`s; repositorios en DI; `InternalsVisibleTo` en Infrastructure. Build 0/0.
- [x] T010 — `20261111175145_AddMafAgent`: solo 2 `CreateTable` + 3 `CreateIndex`; `Down` borra solo esas tablas. `dotnet ef database update` aplicado; `migrations list` ya no la marca como Pending.
- [x] T011 — 9 tests en rojo por la razón correcta: `NotImplementedException: T012` en los 9, con la siembra contra la base funcionando.
- [x] T012 — 9/9 en verde; `grep -rn "ExecuteUpdate" src/Infrastructure/MafAgent` = 3; `TrackedChangesDiscarderTests` 2/2 (control positivo y negativo).

## Control de los gates propios
- Mutante "sin `Status == Pending`" → 4 fallos, incluido `TryClaim_TwoConcurrentCallers_ExactlyOneWins`. La garantía de "como mucho una vez" está probada contra Postgres real.
- Mutante "sin `ExpiresAt > now`" → 2 fallos (caducidad en claim y reject).
- `TrackedChangesDiscarderTests` lleva su control positivo: sin descarte, la misma inserción se guarda (1 fila).

## Acceptance criteria tocados
- AC5 (como mucho una vez, también en concurrencia) — evidencia: `TryClaim_TwoConcurrentCallers_ExactlyOneWins` + mutante.
- AC6/AC7 (caducada / de otro usuario → no se reclama) a nivel de repositorio — evidencia: tests de caducidad y de otro usuario. El comportamiento HTTP llega en T019/T031.

## Capas no ejecutadas
- **SUSTITUIDA** — limpieza de datos de test en la base compartida: `DisposeAsync` borra los usuarios creados (y en cascada sus conversaciones y acciones), pero **no se ha comprobado desde fuera** que no queden filas `maf-it-%` tras un fallo a mitad de test.
- **SUSTITUIDA** — concurrencia: 2 llamadas paralelas por ejecución, no una carga sostenida. No descarta problemas que solo aparezcan con mucha contención.

## Hallazgos descartados
- ninguno

## Verdict: PASS (alcance de tarea T009–T012). Listo para commit.
