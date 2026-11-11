---
type: spec
title: Spec — Agente MAF con tool-calling
description: WHAT y WHY de un segundo agente conversacional, construido con Microsoft Agent Framework, que expone las operaciones de la app como herramientas tipadas, incluidas escrituras con confirmación garantizada.
tags: [sdd, spec, agent, tool-calling]
timestamp: 2026-11-11T12:00:00Z
topic: sdd
slug: maf-tool-calling-agent
status: clarified
---

# Spec — Agente MAF con tool-calling

> Slug: `maf-tool-calling-agent` · Status: clarified — aprobada por el usuario 2026-11-11, clarify cerrado 2026-11-11 · Created: 2026-11-11
> Inherits: [constitution](../constitution.md) — **borrador sin ratificar**; ver *Points to clarify*.

## Problem & why

El único usuario de la app quiere operar sus portafolios hablando: preguntar y también **actuar**
(registrar una compra, corregir una entrada, crear un portafolio, refrescar precios). El agente
actual solo responde preguntas: sus cuatro herramientas son de lectura (en la rama base `development`). Todo cambio de datos
obliga a salir de la conversación y usar la UI o la API a mano.

Además, el agente actual está construido sobre un framework que el proyecto quiere sustituir
por Microsoft Agent Framework. El nuevo agente debe ser el **relevo** del actual. Por eso convive
con él durante un tiempo y cubre, como mínimo, todo lo que el actual ya responde.

## Cost of not building it

- Cada cambio de datos sigue siendo manual, aunque el usuario ya haya expresado la intención en el chat.
- El agente actual queda atado a un framework que el proyecto quiere abandonar. Cada herramienta
  nueva que se le añada aumenta el coste de migrarlo después.
- El coste es moderado, no crítico. Es una app personal de un solo usuario, y la UI ya permite
  hacer todas estas operaciones.

## The cheapest alternative

**Ampliar el agente actual con herramientas de escritura**, sin cambiar de framework. Así se
consigue el "actuar desde el chat" con mucho menos trabajo. No basta porque:
(a) consolida más código sobre el framework que se quiere retirar y encarece el relevo, y
(b) no prueba el framework nuevo antes de depender de él.
Se consideró y se descartó (decisión del usuario, 2026-11-11).

Otra alternativa más barata es un agente MAF **solo de lectura**. Se descartó porque duplicaría
al actual sin aportar capacidades nuevas.

## Goals

- G1 — El usuario puede, en una conversación, consultar todo lo que hoy responde el agente actual
  y además consultar entradas, historial, distribución, catálogos y notas.
- G2 — El usuario puede, desde la conversación, **crear y editar** portafolios, entradas de
  portafolio y notas, y lanzar el refresco de precios y el informe de estado.
- G3 — **Ninguna escritura ocurre sin la confirmación explícita del usuario**. Lo que se ejecuta es
  exactamente lo que se le mostró. Esto no depende de cómo interprete el modelo la conversación.
- G4 — El agente nuevo convive con el actual sin cambiar el comportamiento de este. Así el relevo
  puede decidirse más adelante con evidencia.

## Non-goals / out of scope

- **Borrar** cualquier cosa (portafolios, entradas, notas, catálogos). El agente lo rechaza en v1.
- **Escribir en los catálogos compartidos** (criptomonedas, exchanges). El agente puede leerlos, pero no modificarlos.
- Registro de usuarios e inicio de sesión desde el agente.
- RAG: búsqueda semántica sobre documentos o embeddings. El agente solo trabaja con herramientas.
- Cambios en la página de chat del frontend. En v1 el agente nuevo solo se usa desde la API.
- Retirar o modificar el agente actual. Eso es una decisión diferida.
- Respuestas en streaming. Varios agentes que colaboran. Memoria de largo plazo entre conversaciones.
- Acciones pendientes **agrupadas** que se aplican todas o ninguna (descartado en clarify: una
  acción por escritura).
- Comportamiento distinto según el proveedor del modelo (descartado en clarify: el agente ve lo
  mismo con el modelo local y con el de la nube).

## Users & context

El único usuario de la app, autenticado, que gestiona sus propios portafolios de criptomonedas.
Escribe en lenguaje natural, a menudo con datos sueltos ("compré 0.5 BTC a 60k en Binance ayer").
Espera que el agente entienda la intención, le muestre qué va a hacer y lo haga solo cuando él lo
confirme. Los portafolios, sus entradas, sus acciones pendientes y sus conversaciones pertenecen a
ese usuario. Ningún otro usuario puede verlos ni modificarlos. **Excepción: las notas no tienen
dueño.** El agente las ve y las edita con la misma visibilidad que ya ofrece la API de notas, es
decir, para cualquier usuario autenticado. Los catálogos son compartidos y de solo lectura para el
agente.

## Behaviour

**Conversación**
- El usuario envía un mensaje y recibe una respuesta del agente. Dentro de una misma conversación,
  el agente recuerda los turnos anteriores. Así puede referirse a "ese portafolio" o a una acción
  que propuso antes.
- Cada conversación pertenece a un solo usuario. Un usuario no puede continuar ni leer la
  conversación de otro.
- Una conversación, con sus acciones pendientes, **sobrevive a un reinicio o despliegue de la
  API**. Caduca tras **24 horas sin actividad**. Después, continuarla se rechaza y el usuario
  empieza una nueva.
- Lo que se guarda de una conversación está protegido en reposo al menos como el contenido de las
  notas, porque puede contener texto de notas ya descifrado.
- El agente responde en el idioma en que le escribe el usuario.

**Lectura (main path)**
- Para preguntas sobre datos, el agente usa las operaciones de lectura de la app y responde con
  datos reales del usuario: portafolios, valor, holdings, historial, distribución, entradas,
  pico o valle de un activo, comparación entre fechas, catálogos y notas. Las notas incluyen su
  contenido completo, tanto con el modelo local como con el modelo en la nube.
- Si los datos no existen o no son del usuario, el agente lo dice. Nunca inventa valores.

**Escritura con acción pendiente (main path)**
1. El usuario pide un cambio. Las **operaciones de escritura soportadas** en v1 son ocho:
   crear portafolio, editar portafolio, crear entrada, editar entrada, crear nota, editar nota,
   refrescar precios y enviar el informe de estado. Las dos últimas son acciones de sistema.
2. Si faltan datos obligatorios o son ambiguos (qué portafolio, qué entrada), el agente los pide.
   No propone nada incompleto. Si el activo o el exchange no existen en el catálogo, lo dice y no
   propone nada, porque no puede crearlos.
3. Con los datos completos, el agente **no ejecuta**. Responde con una **acción pendiente** que
   lleva un identificador, la operación y los valores exactos que se aplicarán, y la expiración.
   Las fechas se muestran resueltas y absolutas: "ayer" aparece como fecha y hora concretas, con
   su zona horaria. En una edición, la acción muestra el valor actual y el valor nuevo de cada
   campo que cambia.
4. Para confirmar, el usuario hace una **acción de confirmación distinta del mensaje de chat**.
   Esa acción nombra el identificador de la acción pendiente. Es determinista: ningún modelo
   interpreta texto para decidir si hubo confirmación. Entonces se ejecuta exactamente lo
   propuesto y el usuario recibe el resultado (por ejemplo, el id de la entrada creada).
5. El usuario también puede rechazarla con una acción equivalente, o ignorarla. En ambos casos no
   se escribe nada.
6. Cada escritura ejecutada deja un **registro de auditoría**: qué usuario confirmó, qué acción
   pendiente la autorizó, qué operación y valores se aplicaron, cuándo, y si terminó bien o mal. El
   registro es para diagnóstico del propietario de la app. El usuario no lo ve en la conversación.

**Edge cases**
- Confirmar una acción pendiente ya ejecutada, rechazada o expirada: se rechaza y no se escribe nada.
- Confirmar la misma acción dos veces, incluso a la vez: se ejecuta como mucho una vez.
- Confirmar una acción pendiente de otro usuario: se rechaza igual que si no existiera.
- Si el usuario pide un cambio nuevo mientras hay una acción pendiente, la anterior no se ejecuta
  sola. Sigue siendo confirmable hasta que expire, porque cada acción tiene su propio id.
- Si el usuario pide borrar algo o escribir en un catálogo, el agente explica que no puede hacerlo
  en esta versión. No propone ninguna acción pendiente.
- Si una sola petición implica varias escrituras ("registra estas tres compras"), se genera
  **una acción pendiente por escritura**. Cada una se confirma o rechaza por separado. Confirmar
  dos de tres aplica solo esas dos.
- Si se confirma una **edición** y el recurso cambió por otra vía después de proponerla, la
  confirmación se rechaza, no se escribe nada y el agente ofrece volver a proponerla con los
  valores actuales. Nunca se pisa en silencio un cambio que el usuario no vio.

**Error paths**
- Si la confirmación es válida pero una operación de **crear o editar** falla (validación, el
  portafolio ya no existe), no queda ningún cambio a medias y el usuario recibe el motivo en
  lenguaje claro.
- **Refrescar precios** puede actualizar unos activos y fallar en otros. El resultado lista qué
  activos se actualizaron y cuáles no. Los que sí se actualizaron se conservan.
- **Enviar el informe de estado** no se puede deshacer una vez enviado. El resultado dice si se
  envió o no. Si no se envió, se puede volver a pedir.
- Si el agente no puede completar la respuesta en un tiempo o un número de pasos razonable, el
  usuario recibe un error claro y no se escribe nada.
- Los mensajes de error nunca revelan si existe un recurso de otro usuario.

**Convivencia**
- El agente actual sigue respondiendo igual que antes, en su mismo punto de entrada.

## Acceptance criteria

- AC1 — Given un usuario con portafolios, When hace cualquiera de las cuatro preguntas que hoy
  responde el agente actual en `development` (listar sus portafolios; valor total actual de un
  portafolio; desglose actual por activo; fecha de máximo o mínimo de un activo), Then el agente
  nuevo responde con los mismos datos subyacentes que el actual.
- AC1b — Given un usuario con datos, When pregunta por sus entradas, el historial de un
  portafolio, su distribución, los catálogos de criptomonedas o exchanges, o sus notas, Then la
  respuesta contiene sus datos reales. And si no existen o no son suyos, el agente dice que no
  existen y no inventa valores.
- AC2 — Given un usuario autenticado, When pide cualquiera de las ocho operaciones de escritura
  soportadas con todos sus datos, Then la respuesta contiene una acción pendiente con id,
  operación y valores exactos. And no se ha aplicado ningún cambio todavía.
- AC3 — Given una acción pendiente vigente del usuario para cualquiera de las ocho operaciones,
  When la confirma por su id, Then se aplica exactamente lo mostrado (para crear o editar, los
  mismos valores campo a campo). And la respuesta incluye el resultado (id del recurso creado o
  editado, activos refrescados, informe enviado o no).
- AC4 — Given una acción pendiente, When el usuario envía cualquier mensaje de chat, aunque sea
  "sí", "confirmo" o el propio id escrito en el texto, Then no se escribe nada. Solo la acción de
  confirmación ejecuta.
- AC5 — Given una acción pendiente ya confirmada, When se confirma otra vez (en secuencia o a la
  vez), Then el cambio existe una sola vez.
- AC6 — Given una acción pendiente expirada o rechazada, When se confirma, Then se rechaza y no se
  escribe nada.
- AC7 — Given la acción pendiente del usuario A, When el usuario B intenta confirmarla, Then
  recibe la misma respuesta que con un id inexistente y no se escribe nada.
- AC8 — Given un usuario, When pide borrar un portafolio, una entrada o una nota, o crear o editar
  una criptomoneda o un exchange, Then no se propone ninguna acción pendiente y el agente
  explica que no está soportado.
- AC9 — Given una petición de escritura a la que le falta un dato obligatorio, When el agente
  responde, Then pide el dato y no propone ninguna acción pendiente.
- AC10 — Given una acción pendiente confirmada de crear o editar cuya operación falla, When se
  ejecuta, Then no queda ningún cambio parcial y la respuesta indica el motivo.
- AC10b — Given un refresco de precios confirmado en el que algunos activos fallan, When termina,
  Then la respuesta lista los activos actualizados y los fallidos.
- AC14 — Given una conversación en la que el agente supera su límite de pasos o de tiempo, When
  el usuario espera la respuesta, Then recibe un error claro y no se aplica ningún cambio ni se
  propone ninguna acción pendiente.
- AC11 — Given que el agente nuevo está desplegado, When se usa el agente actual, Then su
  comportamiento y sus tests existentes no cambian.
- AC12 — Given cualquier escritura ejecutada por el agente, When se revisa el registro, Then se puede
  saber qué usuario la confirmó, qué acción pendiente la autorizó y cuándo.
- AC13 — Given una conversación del usuario A, When el usuario B intenta continuarla, Then se
  rechaza sin revelar su contenido.
- AC15 — Given una conversación con una acción pendiente vigente, When la API se reinicia, Then
  la conversación se puede continuar con su contexto And la acción pendiente se puede confirmar.
- AC16 — Given una conversación sin actividad durante más de 24 h, When el usuario intenta
  continuarla o confirmar una de sus acciones pendientes, Then se rechaza y no se escribe nada.
- AC17 — Given una petición que implica N escrituras, When el agente responde, Then hay N acciones
  pendientes independientes. And confirmar un subconjunto aplica solo ese subconjunto.
- AC18 — Given una acción pendiente de edición, When el recurso se modifica por otra vía y después
  se confirma la acción, Then se rechaza, el recurso conserva el cambio externo y la respuesta
  indica que cambió.
- AC19 — Given una petición con una fecha relativa ("ayer"), When se propone la acción pendiente,
  Then muestra la fecha y hora absolutas con su zona horaria.

## Points to clarify

- **suposición tomada** — el agente nuevo se construye con Microsoft Agent Framework. *Base:*
  requisito explícito del usuario y objetivo de relevar al framework actual. *Riesgo:* si MAF no
  admite tool-calling fiable con el modelo local, el objetivo de relevo cambia.
- **suposición tomada** — la constitución es un borrador. Como canon de facto se toma el
  `CLAUDE.md` del repo (Clean Architecture, `ErrorOr`, endpoints por feature, JWT, alcance por
  usuario, sin `SaveChanges` en handlers, `Idempotency-Key` en POST de creación). *Base:* decisión
  del usuario, 2026-11-11. *Riesgo:* si al ratificarla aparece un principio distinto, la spec y el
  plan se revisan.
- **suposición tomada** — una acción pendiente expira a los 10 minutos. *Base:* ventana corta
  habitual para confirmaciones. *Riesgo:* si resulta incómodo, cambia AC6.
- **suposición tomada** — los límites de pasos y tiempo del agente nuevo son equivalentes a los del
  actual. *Base:* paridad para el relevo. *Riesgo:* bajo.
- **suposición tomada** — lo que se guarda de una conversación está protegido en reposo como las
  notas. *Base:* surge de combinar conversación persistida y contenido de notas visible para el
  agente (clarify, 2026-11-11). *Riesgo:* si no hace falta, se simplifica el almacenamiento.
- **suposición tomada** — las fechas relativas se resuelven y se muestran absolutas, con zona
  horaria, antes de confirmar. *Base:* la acción pendiente muestra valores exactos, así que el
  usuario corrige una zona errónea antes de escribir. *Riesgo:* bajo.
- **suposición tomada** — una acción pendiente sigue siendo confirmable hasta que expira, aunque
  el usuario haya pedido otras cosas después. *Base:* cada acción tiene su id, así que no hay
  ambigüedad sobre cuál se confirma. *Riesgo:* si se prefiere que cualquier petición nueva anule
  las anteriores, cambia el edge case y hace falta un AC más.
- **suposición tomada** — la confirmación es una acción del cliente distinta del mensaje de chat.
  *Base:* la opción "acción pendiente" elegida por el usuario: el modelo no puede confirmarse a sí
  mismo. *Riesgo:* el plan define su forma concreta. La página de chat necesitará un botón o algo
  equivalente cuando deje de estar diferida.
- **decisión diferida** — comparación de valores por activo entre dos fechas. Hoy solo existe en
  la rama sin fusionar `feature/portfolio-asset-value-comparison`. Se añadirá al agente MAF cuando
  esa feature llegue a `development`.
- **decisión diferida** — retirar el agente actual y en qué condiciones (criterio de relevo).
- **decisión diferida** — borrar desde el agente, con confirmación.
- **decisión diferida** — usar el agente nuevo desde la página de chat del frontend.
- **área no formulable** — si un modelo local pequeño elige bien entre ~15 herramientas o más.
  Sospecho que habrá que agrupar o reducir herramientas, pero aún no sé formular el umbral sin
  medirlo.

## Clarifications

| Date | Q | Decision | Why |
|------|---|----------|-----|
| 2026-11-11 | ¿Cuánto dura una conversación y sobrevive a un reinicio? | Persistida, sobrevive a reinicios, caduca a las 24 h sin actividad (AC15, AC16). | Un redeploy no debe perder acciones pendientes. 24 h acota retención y almacenamiento. |
| 2026-11-11 | ¿Puede el agente leer el contenido de las notas y enviarlo al modelo? | Sí, con Ollama local y con Azure OpenAI. | Crear y editar notas sin ver su contenido no tiene sentido. El usuario ya dicta el contenido en el chat. |
| 2026-11-11 | ¿El informe de estado necesita más que la acción pendiente? | No. Resuelto en el código. | `SendPortfolioStatusReportCommandHandler` solo envía al email del propio usuario. No llega a terceros. |
| 2026-11-11 | ¿Varias escrituras en una petición? | Una acción pendiente por escritura, confirmable por separado (AC17). | Casa con "todo o nada por operación" y no exige transacciones multi-`SaveChanges`. |
| 2026-11-11 | ¿Idempotencia de creaciones vía agente? | Resuelto en la spec. El id de la acción pendiente es la clave de idempotencia (AC5). | El `IdempotencyFilter` protege reintentos HTTP de POST. Aquí la garantía es "como mucho una vez por id". |
| 2026-11-11 | Hueco nuevo: ¿qué pasa si un recurso cambia entre la propuesta y la confirmación de una edición? | Se rechaza y se ofrece volver a proponer (AC18). | Nunca pisar en silencio un cambio que el usuario no vio. |
| 2026-11-11 | (Surgida en `plan`) Las notas no tienen dueño en el sistema (sin usuario, partición por categoría). ¿Cómo las trata el agente? | Con la misma visibilidad que la API de notas: cualquier usuario autenticado. AC1b, en "no son suyos", no aplica a las notas. | Decisión del usuario. Es una app de un solo usuario, y dar dueño a las notas sería scope nuevo con migración. |
| 2026-11-11 | (Surgida en analyze, ejecución 3) AC1 prometía paridad con 5 preguntas, pero en `development` el agente SK solo tiene 4; la comparación entre fechas vive en otra rama sin fusionar. | AC1 = 4 preguntas. La comparación pasa a decisión diferida. | Decisión del usuario (opción a). No acopla esta feature a otra rama ni duplica su lógica de Domain. El error vino de leer en specify la carpeta principal, que estaba en otra rama. |
| 2026-11-11 | Defaults asumidos sin objeción: fechas absolutas con zona (AC19), antes→después en ediciones, activo o exchange inexistente → no hay propuesta, respuesta en el idioma del usuario, conversación protegida en reposo como las notas. | Asumidos. | Valores seguros por defecto. Visibles en la acción pendiente antes de escribir. |

### Resultado por punto del handoff

- MAF como framework — **validada, se mantiene**.
- Constitución en borrador / CLAUDE.md como canon — **validada, se mantiene**. Sigue siendo un riesgo para `plan`.
- Expiración de 10 min de la acción pendiente — **validada, se mantiene** (dentro de la ventana de 24 h de la conversación).
- Límites de pasos y tiempo con paridad — **validada, se mantiene**.
- Acción pendiente confirmable hasta expirar — **validada, se mantiene**.
- Confirmación como acción distinta del chat — **validada, se mantiene**.
- Duración de la conversación — **preguntada → persistida 24 h**.
- Contenido de notas al modelo — **preguntada → sí, a ambos**. El fallback se elimina.
- Informe hacia fuera — **resuelta en el código**.
- Varias escrituras — **preguntada → una por escritura**.
- Idempotencia — **resuelta en la spec (AC5)**.
- Retirar SK, borrado, frontend — **decisiones diferidas, se dejan**.
- Modelo pequeño con ~15 herramientas — **área no formulable, se mantiene**. Graduará cuando el plan mida la selección de herramientas.

## Revisions

| Date | Change | Why |
|------|--------|-----|
| 2026-11-11 | Draft inicial. | Fase `specify`, decisiones del usuario en tres rondas de frontera. |
| 2026-11-11 | Revisión con ojos frescos: confirmación determinista fuera del chat, registro de auditoría en Behaviour, AC2/AC3 generalizados a las 8 escrituras, lista explícita de las 5 preguntas (AC1), AC1b (lecturas nuevas), AC10 limitado a crear/editar más AC10b (refresco parcial) y AC14 (límites), fallback de notas y ciclo de vida de acciones pendientes. | 8 hallazgos de un revisor con contexto limpio; todos se aceptaron sin cambiar decisiones del usuario. |
| 2026-11-11 | Spec aprobada por el usuario ("la apruebo"). | Gate de `specify` (paso 9) superado en modo con paradas. |
| 2026-11-11 | AC1 reducido a 4 preguntas; comparación entre fechas diferida (analyze ejecución 3, H1). | Premisa falsa sobre la rama base; decisión del usuario. |
| 2026-11-11 | Clarify: 4 decisiones del usuario, 2 puntos resueltos internamente, 1 hueco nuevo (desfase en ediciones), AC15–AC19, status → clarified. | Fase `clarify`. |
