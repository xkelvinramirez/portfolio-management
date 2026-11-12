---
type: verification
title: Verification — maf-tool-calling-agent — 2026-11-11 — T037–T038 (pre-commit)
description: Verificación de alcance de tarea antes del commit del registro de ids insertados (AddedEntityIdLog). No es el veredicto de la feature.
tags: [sdd, verification]
timestamp: 2026-11-12T02:30:00Z
topic: sdd
slug: maf-tool-calling-agent
source_state: d613cbf on feature/maf-tool-calling-agent-pr2 (dirty — the T037–T038 changes under verification)
---

# Verification — maf-tool-calling-agent — 2026-11-11 — T037–T038

## Stack gate (config.yaml; una ejecución tras la última edición)
- build → PASS (0 avisos, 0 errores)
- format `--verify-no-changes` → PASS (exit 0)
- unit → PASS — 193/193 (188 + 5 `AddedEntityIdLogTests`)
- integration → PASS — 11/11 (10 del reclamo + 1 `AddedEntityIdLogIntegrationTests`) contra el Postgres de desarrollo

## Task done-checks
- [x] T037 — tests escritos en rojo (CS0246: `AddedEntityIdLog` ausente). Cubren: id asignado tras insertar, null sin inserciones, la última de dos inserciones del mismo tipo, tipos que no se mezclan, y que las actualizaciones no cuentan como inserciones.
- [x] T038 — `AddedEntityIdLog : SaveChangesInterceptor, IAddedEntityIdLog`, scoped, registrado en `AppDbContext` con `AddInterceptors` y expuesto como `IAddedEntityIdLog`. `UNIT` verde sin cambios en los tests existentes.

## Control de los gates propios (hallazgo)
- Mutante "registrar los ids ANTES del commit": **sobrevive** a los 5 tests InMemory, porque el proveedor InMemory asigna los ids al hacer `Add`. Con él, el done-check de T037 tal como estaba escrito **no podía distinguir** el problema G2.
- Corrección: `AddedEntityIdLogIntegrationTests` contra Postgres comprueba `Id == 0` antes de `SaveChanges` y el id real después. El mismo mutante **falla** ("Expected log.LastAddedId<Portfolio>() to be 5L, but found 0L").
- Los tests InMemory se conservan: cubren la lógica de registro (tipos, última inserción, actualizaciones), no el momento del commit.

## Riesgo nuevo detectado y resuelto
- Con dos clases de integración, el barrido de residuos de una podía borrar usuarios vivos de la otra si xUnit las ejecutaba en paralelo. Ahora todas las clases que usan la base de desarrollo están en la colección `DevDatabase` (`DisableParallelization = true`). Helper común: `DevDatabase` (cadena de conexión, usuario de test y barrido).

## Acceptance criteria tocados
- AC3 ("la respuesta incluye el id del recurso creado") — la base está probada aquí (el id real tras el commit). El extremo a extremo llega con T017/T033.

## Capas no ejecutadas
- **SUSTITUIDA** — limpieza de la base compartida: barrido por prefijo al iniciar más borrado en `DisposeAsync`. No se ha consultado desde fuera que no quedan filas.

## Hallazgos descartados
- ninguno

## Verdict: PASS (alcance de tarea T037–T038). Listo para commit.
