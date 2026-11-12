---
type: summary
title: Resumen — maf-tool-calling-agent
description: Qué se construyó en el agente MAF con tool-calling, cómo se entregó, qué se decidió y qué queda pendiente.
tags: [sdd, summary, maf-agent]
timestamp: 2026-11-12T22:00:00Z
topic: sdd
slug: maf-tool-calling-agent
source: 02-DOCS/raw/worklog/2026-11-12-maf-tool-calling-agent.md
---

# Resumen — maf-tool-calling-agent

## Qué es
Un segundo agente de chat, construido con Microsoft Agent Framework 1.28, que convive con el agente Semantic
Kernel sin tocarlo. Lee los datos del usuario con herramientas y **propone** escrituras. Nada se escribe hasta que
el usuario confirma la acción pendiente por su id, con el botón de la app. Flujo completo en
[`docs/maf-agent-flow.md`](../../../../docs/maf-agent-flow.md).

## Qué se construyó (T001–T041)
- **Dominio y persistencia:**
  - `MafConversation` expira a las 24 h y `PendingAction` a los 10 min.
  - La sesión y el payload se guardan cifrados en Postgres.
  - Confirmar y rechazar reclaman la acción de forma atómica (`ExecuteUpdate`).
- **Application:**
  - Factoría de acciones, detección de desfase en ediciones, lector de valores actuales y ejecutor.
  - Handlers de chat, confirmar y rechazar.
- **Herramientas:** 12 de lectura y 8 `Propose*`. Los errores vuelven como texto para el modelo: `NotFound`,
  `MissingOrAmbiguous:<campo>` y `UnknownCatalogItem:<valor>`.
- **Runner:**
  - Cada turno recibe la hora UTC y la de Madrid, y los portafolios del usuario con su id.
  - Un middleware cuenta las llamadas a herramientas y corta al pasar el límite.
  - El timeout de inferencia se convierte en un error propio.
- **HTTP:** `POST /api/v1/MafAgent/chat`, `.../pending-actions/{id}/confirm` y `.../reject`.

## Cómo se entregó
- Cinco PRs apilados, del PR1 (`feature/maf-tool-calling-agent`) al PR5 (`-pr5`). Cada bloque pasó por:
  - test en rojo, verde y mutantes;
  - registro de verificación;
  - revisión de un subagente;
  - gate completo antes del commit.
- Estado final: unit 471/471, integración 12/12 contra Postgres y regresión SK 11/11.
- Checklist con modelo real: 9/9 con `qwen2.5:7b-instruct`
  ([verificación](../verifications/maf-tool-calling-agent-2026-11-12-T035-T036.md)).

## Decisiones clave
En [harness/decisions.md](../../harness/decisions.md): fechas como texto, fecha sin hora = día UTC, límite de pasos
por middleware, quién corta y reintenta, portafolios en el contexto del turno, y 7B como modelo mínimo.

## Lecciones
- Un modelo de 3B no sigue instrucciones de tool-calling sin ayuda: inventa ids y datos. Darle los ids en el
  contexto lo arregló para la lectura. Para las escrituras hizo falta 7B.
- Las garantías deterministas aguantaron incluso con el modelo fallando. Ningún caso escribió datos ni dejó una
  acción con datos inventados.
- Las capas de timeout apiladas (HttpClient, SDK de OpenAI, Polly) multiplican la espera. Solo una capa debe
  cortar y reintentar.

## Pendiente
- Opcional: que las herramientas `Propose*` devuelvan todos los campos que faltan a la vez.
- La checklist es manual: no detecta la variación del modelo entre ejecuciones.
