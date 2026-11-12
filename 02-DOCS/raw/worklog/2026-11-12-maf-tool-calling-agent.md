# Worklog — maf-tool-calling-agent — 2026-11-11 / 2026-11-12

Registro en bruto de la implementación. La versión compilada está en
`02-DOCS/wiki/sdd/summaries/maf-tool-calling-agent.md`.

## 2026-11-11
- Pre-implement: T039 (regresión SK), T040–T041 (los logs ocultan Message, Reply, Values, Before, Result, Content, Summary).
- T001: paquetes. MAF 1.28.0; MEAI 10.5→10.11 y OpenAI 2.10→2.14 transitivos aceptados; SK 1.81.0 sin cambios.
- T003–T008: dominio (`MafConversation` 24 h, `PendingAction` 10 min), contratos e interfaces.
- T009–T012: persistencia en Postgres, enums como texto, reclamo atómico con `ExecuteUpdate`.
- T037–T038, T002: interceptor de ids añadidos y smoke test.
- T013–T020: factoría de acciones, detección de desfase, lector de valores actuales, ejecutor y handler de confirmar.
- PR1, PR2 y PR3 abiertos como ramas apiladas.

## 2026-11-12
- T021–T022: rechazar. T023–T024: chat y DI. Push de pr3.
- PR4: T025–T026 herramientas de lectura (12), T027–T028 herramientas de propuesta (8), T029–T030 runner MAF y DI.
  - Revisión T027: una fecha sin hora como fin de periodo perdía el último día. Decisión del usuario: fecha sin hora = día UTC.
  - Revisión T029–T030 (FAIL importante): timeouts de 100 s del HttpClient y de OpenAI más reintentos → ~400 s y un 500. Ahora solo el pipeline de inferencia corta y reintenta.
- PR5: T031–T032 endpoints, T033 reinicio contra Postgres, T034–T036 regresión SK, documentación y gate.
- Checklist manual con Ollama (T035):
  - 3B: 9/9 FALLO (ids inventados, datos inventados, idioma, confirmación de borrado ofrecida).
  - Refuerzo (a77e8ae): portafolios con id en el contexto de cada turno y reglas explícitas. 3B: 6/9 OK.
  - 7B: 9/9 OK. T035–T036 cerradas en PASS (4981bea).
- `qwen2.5:7b-instruct` en los user-secrets del usuario (`Ai:ReasoningModel:ModelId`).
