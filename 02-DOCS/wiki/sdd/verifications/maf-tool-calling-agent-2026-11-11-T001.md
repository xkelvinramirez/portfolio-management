---
type: verification
title: Verification — maf-tool-calling-agent — 2026-11-11 — T001 spike (pre-commit)
description: Verificación de alcance de tarea antes del commit de T001 (paquetes MAF + spike). No es el veredicto de la feature.
tags: [sdd, verification]
timestamp: 2026-11-11T23:59:00Z
topic: sdd
slug: maf-tool-calling-agent
source_state: afa3f3d (dirty — T001 changes under verification: Infrastructure.csproj, MafSpikeTests.cs, docs/maf-agent-packages.md, decisions.md)
---

# Verification — maf-tool-calling-agent — 2026-11-11 — T001

> Alcance: **tarea**. La feature tiene la mayoría de tareas sin implementar.

## Stack gate (config.yaml, una ejecución tras la última edición)
- format `--verify-no-changes` → PASS (exit 0)
- build → PASS (0 avisos, 0 errores)
- unit → PASS — 161/161, 0 omitidos (incluye T039 `SkAgentRegressionTests` 11/11 y las 2 pruebas offline del spike)
- integration (`--ignore-exit-code 8`) → exit 0, 0 tests (NO-APLICA aún)
- `dotnet list src/Infrastructure package --vulnerable --include-transitive` → sin paquetes vulnerables

## Task done-check T001
- [x] build exit 0 con MAF 1.28.0 y SK 1.81.0 a la vez, sin cambiar la versión de SK (`git diff` del `.csproj`: solo 2 líneas añadidas).
- [x] `UNIT` verde (161/161).
- [x] `MafSpikeTests` (Trait `Manual`) verdes contra Ollama, dos ejecuciones: 3/3 y 3/3 (en caliente 6–9 s por prueba). Clase en `tests/WebApi.MinimalAPI.Tests/MafAgent/Spike/`.
- [x] Ida y vuelta de sesión: serializar → `DeserializeSessionAsync` en una instancia nueva del agente → el segundo turno recupera el valor del primero.
- [x] Cliente Azure OpenAI construido sin red y envuelto como agente MAF (prueba offline).
- [x] Fila "MAF package versions" en `decisions.md`.
- [x] `docs/maf-agent-packages.md` con la lista antes y después. Cambio transitivo MEAI 10.5→10.11 y OpenAI 2.10→2.14 **aprobado por el usuario**.
- [x] T039 sigue verde.

## Hallazgos del spike (entrada para tareas posteriores)
- `FunctionInvokingChatClient.MaximumIterationsPerRequest` **no lanza** al llegar al límite: corta y devuelve una respuesta (prueba determinista `FunctionInvocationLimit_ObservedBehaviour…`). Confirma plan §0 "Límite de iteraciones" y T029/T030.
- La llamada real a función de SK contra Ollama funciona con MEAI 10.11 cargado (`SkAgent_LiveFunctionCalling…`).
- Modelo: `qwen2.5:3b-instruct` es el único instalado en Ollama, declara la capacidad `tools`, tiene 32K de contexto y corre solo en CPU (`size_vram: 0`, ~2,1 GB). Con 2 herramientas elige bien. Con las 19 del plan no está medido: lo decide la checklist de T035.

## Capas no ejecutadas
- **NO-APLICA** — integration: no hay tests `Category=Integration` todavía.
- **SUSTITUIDA** — fiabilidad del modelo: 2 ejecuciones manuales en lugar de una medición estadística. No detecta fallos intermitentes de selección de herramientas con baja frecuencia.

## Hallazgos descartados
- ninguno

## Verdict: PASS (alcance de tarea T001). Listo para commit.
