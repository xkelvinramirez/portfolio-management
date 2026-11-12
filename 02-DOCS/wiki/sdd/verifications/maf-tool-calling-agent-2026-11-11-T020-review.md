---
type: verification
title: Verification — maf-tool-calling-agent — 2026-11-11 — arreglo de la revisión de T017–T020 (pre-commit)
description: Verificación del arreglo del hallazgo importante de la revisión de T017–T020 (nota de sesión fuera de la captura). No es el veredicto de la feature.
tags: [sdd, verification]
timestamp: 2026-11-12T07:00:00Z
topic: sdd
slug: maf-tool-calling-agent
source_state: e02d3b8 on feature/maf-tool-calling-agent-pr3 (dirty — the fix under verification)
---

# Verification — arreglo de la revisión de T017–T020

## Stack gate (ejecución nueva tras la última edición)
- format → PASS · build → PASS (0 avisos) · unit → PASS 296/296 · integration → PASS 11/11

## Hallazgo importante (revisión: VERDICT fail)
- Si la nota de sesión lanzaba (sesión corrupta, error del runner) después de que el comando hubiera escrito, confirm devolvía 500 y la acción quedaba en `Executing` aunque la escritura existía. Rompía "tras el reclamo, nunca un error".
- Arreglo: la nota entra en el mismo `try`. Una sola captura de `Exception` con dos ramas: con el desenlace ya decidido (`when (outcome is not null)`) se registra un aviso, se omite la nota y se devuelve el desenlace real, que la UoW persiste; sin desenlace, la rama de siempre (DiscardAll + TryMarkFailed). Además se registra un aviso si `TryMarkFailedAsync` no encuentra la fila en `Executing` (menor #2).
- Evidencia roja: el handler de `e02d3b8` con los tests nuevos → 2 fallos (fallo de nota, aviso de TryMarkFailed). Con el arreglo → 24/24.

## Mutantes que el revisor vio sobrevivir, ahora detectados
- `IsEdit` siempre verdadero → 9 fallos · `TryMarkFailedAsync` con el token de la petición → 1 · sin comprobar conversación nula → 1 · sin la rama de fallo de nota → 1.
- Cubiertos además: `DecidedAt` y nota en la rama `Stale`.

## Pendiente para T031 (menor #3)
- El endpoint `confirm` debe enviar el comando sin token (`sender.Send(command)`): el reclamo usa el token de la petición, y una cancelación tras su commit daría 500 con la fila en `Executing`. T031 debe comprobarlo.

## Hallazgos descartados
- ninguno

## Verdict: PASS. Listo para commit.
