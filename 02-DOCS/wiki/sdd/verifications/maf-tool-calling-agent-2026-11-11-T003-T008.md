---
type: verification
title: Verification — maf-tool-calling-agent — 2026-11-11 — T003–T008 (pre-commit)
description: Verificación de alcance de tarea antes de los commits de dominio, contratos y puertos del PR2. No es el veredicto de la feature.
tags: [sdd, verification]
timestamp: 2026-11-12T00:30:00Z
topic: sdd
slug: maf-tool-calling-agent
source_state: ec54b8a (dirty — 9 paths, the T003–T008 changes under verification)
---

# Verification — maf-tool-calling-agent — 2026-11-11 — T003–T008

## Stack gate (config.yaml; una ejecución tras la última edición)
- build → PASS (0 avisos, 0 errores)
- format `--verify-no-changes` → PASS (exit 0)
- unit → PASS — 186/186 (165 previos + 21 nuevos de Domain)
- integration (`--ignore-exit-code 8`) → exit 0, 0 tests (NO-APLICA aún)

## Task done-checks
- [x] T003 — `MafConversationTests` en rojo por tipo ausente (error CS0246 al compilar), como define el done-check.
- [x] T004 — `MafConversationTests` 6/6. Bordes: 24 h exactas → no expirada; +1 tick → expirada.
- [x] T005 — `PendingActionTests` en rojo por tipo ausente (CS0246).
- [x] T006 — `PendingActionTests` 15/15: `Create` (+10 min), `IsExpired`, transiciones válidas e inválidas, y las 8 operaciones.
- [x] T007 — DTOs de plan §3 en `src/Contracts/MafAgent/`. Compila.
- [x] T008 — puertos en `src/Application/MafAgent/Interfaces/` (`IMafConversationRepository`, `IPendingActionRepository` con TryClaim/TryReject/TryMarkFailed, `ITrackedChangesDiscarder`, `IAddedEntityIdLog`, `ICurrentValuesReader`, `IMafAgentRunner` + `MafAgentTurnResult`), todos `public`. `InternalsVisibleTo` (Application.Tests, WebApi.MinimalAPI.Tests, DynamicProxyGenAssembly2). Sin referencias directas a MAF ni a MEAI en Application.

## Control de los gates propios
- **Mutación** de los operadores de caducidad (`>` ↔ `>=` en `MafConversation.IsExpired`; `>=` ↔ `>` en `PendingAction.IsExpired`): 2 tests fallan (`…ExactlyTwentyFourHours…IsNotExpired`, `…AtTenMinutes_IsExpired`). Restaurado: verde.
- **Check de referencias prohibidas** (`dotnet list package` + grep estructurado con 3 salidas): control positivo en Application → exit 0; control negativo en Infrastructure (sí tiene MAF) → exit 1; un fallo del escaneo → exit 2. Límite: solo mira referencias **directas** (la regla del plan), no transitivas.

## Acceptance criteria
- Sin AC completo en este bloque: son cimientos de AC5/AC6/AC12/AC15/AC16, que se verifican cuando lleguen sus handlers y su persistencia.

## Capas no ejecutadas
- **NO-APLICA** — integration: no hay tests de integración todavía.

## Hallazgos descartados
- ninguno

## Verdict: PASS (alcance de tarea T003–T008). Listo para commit.
