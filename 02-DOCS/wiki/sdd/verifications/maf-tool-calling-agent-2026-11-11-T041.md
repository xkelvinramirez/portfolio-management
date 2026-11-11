---
type: verification
title: Verification — maf-tool-calling-agent — 2026-11-11 — T040/T041 (pre-commit)
description: Verificación de alcance de tarea antes del commit de T041 (redacción en LoggingBehavior). No es el veredicto de la feature.
tags: [sdd, verification]
timestamp: 2026-11-11T23:30:00Z
topic: sdd
slug: maf-tool-calling-agent
source_state: 6dc1934 (dirty — 1 file: src/Application/Common/Behaviors/LoggingBehavior.cs, the T041 change under verification)
---

# Verification — maf-tool-calling-agent — 2026-11-11 — T040/T041

> Alcance: **tarea** (pre-commit de T041). La feature tiene la mayoría de tareas sin implementar; su verificación completa contra los AC de la spec es posterior (fin de `implement`).

## Stack gate (config.yaml `testing.commands.verify`, una sola ejecución tras la última edición)
- `dotnet format portfolio-management.slnx --verify-no-changes` → PASS (exit 0)
- `dotnet build portfolio-management.slnx` → PASS (0 avisos, 0 errores)
- unit (`--filter-not-trait Category=Integration --filter-not-trait Category=Manual`) → PASS — 159/159, 0 omitidos
- integration (`--filter-trait Category=Integration`) → exit 8 = "zero tests ran" (MTP). Ver *Capas no ejecutadas*.

## Task done-checks
- [x] T040 — test rojo por la razón correcta: en 6dc1934 `Handle_NeverLogsSensitiveFreeTextProperties_IncludingNestedCollections` falló por aserción mostrando los 10 centinelas en el log (incl. `PendingActions[]` anidado). Guardas (`StillLogsNonSensitiveProperties`, `KeepsRedactingExistingCredentialProperties`) verdes.
- [x] T041 — T040 verde (3/3) con el cambio de `SensitivePropertyNames`; `UNIT` verde (159/159).

## Control del propio gate (home-grown)
- Negativo: el test de T040 se vio fallar contra el código sin el cambio (6dc1934).
- Positivo: el mismo test pasa con el cambio, y `StillLogsNonSensitiveProperties` demuestra que la redacción no oculta todo (las propiedades no sensibles siguen en el log).
- Límite: la redacción es **por nombre de propiedad**. Una propiedad sensible con otro nombre (p. ej. `Text`, `Body`) no se ocultaría. Los contratos nuevos de MafAgent deben usar los nombres de la lista (plan §0).

## Acceptance criteria (spec) — solo los tocados por esta tarea
- [x] Behaviour §Conversación "lo que se guarda… protegido" — parte de logs MediatR: los nombres `Message`, `Reply`, `Values`, `Before`, `Result`, `Content`, `Summary` no llegan a Seq (test T040).
- [ ] (fuera de alcance de esta tarea) Que `Summary` no repita texto libre lo cubre T027.

## Capas no ejecutadas
- **NO-APLICA** — integration: todavía no existe ningún test `Category=Integration` (llegan en T011). MTP devuelve exit 8 con cero tests.
- **Defecto del gate detectado (over-blocking):** el comando `integration` de `config.yaml` falla siempre mientras no haya tests de integración. Recomendado: añadir `--minimum-expected-tests 0` o tratar el exit 8 como no aplicable, en `config.yaml` (fase tasks/sdd-init). No se parchea aquí.

## Hallazgos descartados
- "Redactar `Message` o `Result` de forma global oculta información de diagnóstico útil": descartado para este alcance. Las respuestas de error usan `ErrorOr` (`Errors[].Code`/`Description`), no `Message`, y la suite completa (159) sigue verde. Queda como efecto transversal conocido (decisions.md, post-analyze 3).

## Verdict: PASS (alcance de tarea T040/T041). Listo para commit. La verificación de la feature queda pendiente.
