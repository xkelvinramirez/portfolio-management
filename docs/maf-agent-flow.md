# Agente MAF con tool-calling (`MafAgent`)

## Contexto

Segundo agente de chat de la API, construido con **Microsoft Agent Framework (MAF) 1.28**. Convive con el
agente de Semantic Kernel (`/api/v1/Agent/chat`, ver `docs/agent-chat-flow.md`), que no cambia. El agente MAF
responde preguntas sobre los portafolios del usuario autenticado y además puede **proponer escrituras**: crear
o editar portafolios, entradas y notas, refrescar precios y enviar el informe de estado.

La regla central: **el modelo nunca escribe**. Solo propone. Cada escritura queda como una *acción pendiente*
que el usuario confirma (o rechaza) por id, fuera del chat. Ningún texto del chat ("sí", "confirmo", el id
escrito) ejecuta nada.

Spec: `02-DOCS/wiki/sdd/specs/maf-tool-calling-agent.md`. Plan: `02-DOCS/wiki/sdd/plans/maf-tool-calling-agent.md`.
Versiones de paquetes: `docs/maf-agent-packages.md`.

## Endpoints

Grupo `/api/v1/MafAgent/`, con autenticación obligatoria. Ningún POST lleva `Idempotency-Key`: confirmar y
rechazar son idempotentes por id, y un chat duplicado solo duplica propuestas inofensivas.

| Método y ruta | Respuesta | Errores |
| --- | --- | --- |
| `POST chat` `{ conversationId?, message }` | 200 `{ conversationId, reply, pendingActions[] }` | 404 `MafAgent.Conversation.NotFound` (no existe o es de otro usuario) · 409 `MafAgent.Conversation.Expired` (más de 24 h sin actividad) · 400 `MafAgent.LoopExhausted` / `MafAgent.Timeout` / validación (mensaje de 1 a 2000 caracteres) |
| `POST pending-actions/{id}/confirm` | 200 `{ id, operation, status: Succeeded \| Failed \| Stale, result?, error? }` | 404 `MafAgent.PendingAction.NotFound` (también la de otro usuario) · 409 `MafAgent.PendingAction.Expired` / `MafAgent.PendingAction.AlreadyDecided` |
| `POST pending-actions/{id}/reject` | 204 | 404 / 409, igual que confirmar |

`pendingActions` contiene **solo** las acciones propuestas en ese turno. Cada una lleva `values` (la petición
completa que se ejecutará, fechas en UTC con `Z`), `before` (estado actual, solo en ediciones), `summary` (sin
texto libre del usuario, con la hora local de Madrid) y `expiresAt` (10 minutos).

## Flujo A — proponer (chat)

1. `SendMafAgentMessageCommand` carga la conversación (o crea una si `conversationId` es null). Comprueba dueño
   y caducidad **antes** de descifrar la sesión.
2. `MafAgentRunner` restaura la sesión MAF desde el JSON descifrado (vacío = nueva), sustituye el mensaje de
   contexto horario del turno anterior por el actual (hora UTC, hora local de `Europe/Madrid` con su offset e id
   de zona) y ejecuta el turno.
3. El modelo llama a herramientas:
   - **Lectura** (12): las 5 heredadas del agente SK (`ListMyPortfolios`, `GetPortfolioValue`,
     `GetPortfolioHoldings`, `GetAssetPeakOrTroughDate`, `GetPortfolioAssetValueComparison`), que devuelven
     los mismos datos que el plugin SK, más entradas, historial, reparto, catálogos y notas. Nunca exponen la
     API key de un exchange, y el listado de notas no incluye su contenido.
   - **Propuesta** (8): `ProposeCreatePortfolio`, `ProposeUpdatePortfolio`, `ProposeCreateEntry`,
     `ProposeUpdateEntry`, `ProposeCreateNote`, `ProposeUpdateNote`, `ProposeRefreshPrices`,
     `ProposeSendStatusReport`. Solo envían consultas y preparan **una** `PendingAction` con `Add`.
4. El runner devuelve la respuesta, la sesión serializada y las acciones del turno. El handler cifra la sesión y
   la unidad de trabajo guarda la conversación y las acciones juntas. **Si el turno falla, no se guarda nada.**

Ninguna herramienta recibe `userId`: la identidad sale siempre del JWT (`ICurrentUserProvider`).

### Respuestas de las herramientas de propuesta

| Respuesta | Significado | El agente debe… |
| --- | --- | --- |
| resumen de la acción | se preparó una acción pendiente | decir qué se propuso y que hay que confirmarlo |
| `MissingOrAmbiguous:<campo>` | falta un dato, es inválido o es ambiguo | preguntar por ese campo |
| `UnknownCatalogItem:<valor>` | el activo o el exchange no existe en el catálogo | decirlo |
| `NotFound` | el recurso no existe o no es del usuario | decirlo |
| `MissingOrAmbiguous:changes` | la edición no cambia nada | decirlo |

Las ediciones reciben **solo lo que cambia**. La herramienta lee el estado actual (`ICurrentValuesReader`), lo
fusiona y guarda la petición completa; `before` es ese estado actual. Así, por ejemplo, editar el título de una
nota inactiva la mantiene inactiva.

### Fechas

Las herramientas reciben las fechas como **texto**:

- con `Z` o con offset → ese instante;
- fecha y hora sin offset → hora local de `Europe/Madrid`;
- **fecha sin hora** (`2026-11-10`) → ese día a las 00:00 UTC (decisión del usuario, 2026-11-12: Domain e
  informes agrupan por día UTC, y la medianoche de Madrid cae en el día UTC anterior).

No se usa `DateTimeOffset` porque System.Text.Json leería una fecha sin offset en la zona horaria del servidor.
Todo payload guarda instantes con `Kind=Utc`. El informe sin periodo usa el mes natural anterior, calculado al
proponer.

## Flujo B — confirmar

1. `ConfirmPendingActionCommand` (el endpoint lo envía **sin** token de cancelación). Comprueba, en este orden:
   dueño (404), caducidad de la acción o de la conversación (409), estado `Pending` (409).
2. **Reclamo atómico** (`TryClaimAsync`, un `UPDATE … WHERE status = Pending`): de dos confirmaciones
   simultáneas solo una gana. Una confirmación y un rechazo tampoco pueden ganar los dos.
3. Desde aquí **nunca se devuelve un error**: todo desenlace es un 200 con su `status`, para que la unidad de
   trabajo persista el estado final.
   - Ediciones: si el recurso cambió desde la propuesta → `Stale`, sin escribir nada.
   - `PendingActionExecutor` despacha el **comando existente** (crear entrada, editar nota…) con
     `CancellationToken.None`. Sus validaciones y su pipeline son los de siempre. Un fallo de negocio → `Failed`
     con el código del comando. En las creaciones con id de base de datos, el id real sale de `AddedEntityIdLog`
     tras el commit.
   - Una excepción inesperada → se vacía el tracker de EF, la acción pasa a `Failed`
     (`MafAgent.PendingAction.UnexpectedFailure`) y se devuelve 200. Es el **único** `catch (Exception)` de la
     feature.
4. Se añade a la sesión una nota de sistema con el resultado (ids y estado, sin texto libre). Si eso falla, se
   conserva el desenlace real sin la nota.

Si el proceso se cae entre el reclamo y el commit final, la acción queda en `Executing` para siempre: se prefiere
"como mucho una vez" a "al menos una vez".

## Flujo C — rechazar

`RejectPendingActionCommand` hace la transición atómica `Pending → Rejected` (`TryRejectAsync`) sin tocar la
entidad cargada. No pasa por la unidad de trabajo y no escribe datos de negocio.

## Límites y errores

- **Límite de pasos:** un middleware cuenta las llamadas a herramientas del turno. Al superar
  `Ai:MaxAllowedToolLoops` (5) no ejecuta la herramienta, corta el bucle y el turno termina en
  `MafAgent.LoopExhausted`.
- **Timeout:** solo el pipeline de Polly del `HttpClient` "InferenceClient" (`Ai:RequestTimeoutSeconds`) corta y
  reintenta. El `HttpClient` y el cliente de OpenAI tienen sus propios timeouts y reintentos desactivados. Un
  corte que el cliente no pidió → `MafAgent.Timeout`.
- **Caducidad:** acción pendiente 10 minutos; conversación 24 horas sin actividad.

## Seguridad y logs

- La sesión, los payloads, el `before` y el resultado se guardan **cifrados** (AES, `IEncryptionService`).
- El log de herramientas registra nombre, conversación, duración y éxito o fallo (solo el tipo de excepción).
  Nunca argumentos, resultados ni el texto del mensaje. `LoggingBehavior` oculta `Message`, `Reply`, `Values`,
  `Before`, `Result`, `Content` y `Summary`.
- Las tablas son `mafagent_conversations` y `mafagent_pending_actions`.

## Configuración

Reutiliza la sección `Ai` (`AiOptions`) y el modelo de razonamiento (`Ai:ReasoningModel`):

- `Ai:Provider = Ollama` u `OpenAI` → cliente compatible con OpenAI. Ollama exige `Ai:ReasoningModel:Endpoint`
  (por ejemplo `http://localhost:11434/v1`); sin él la app falla en vez de llamar a api.openai.com.
- `Ai:Provider = AzureOpenAI` → cliente de Azure con `Ai:ReasoningModel:DeploymentName` y `Endpoint` obligatorio.
- Claves y cadenas de conexión solo con `dotnet user-secrets`.

## Decisiones

- **Acciones pendientes confirmadas por id**, nunca por texto del chat (AC4).
- **Payloads tipados con la petición completa**, también en ediciones, para ejecutar y comparar exactamente lo
  que se propuso.
- **Detección de desfase** al confirmar ediciones, con el mismo lector y formato que se usó al proponer.
- **Fechas como texto** y fecha sin hora = día UTC (ver arriba).
- **Sin `Idempotency-Key`** en estos POST (desviación aceptada, analyze F5).
- **El agente SK no se toca** ni cambia su versión (1.81.0).

## Comprobación manual con un modelo real (T035)

Con Ollama y `qwen2.5:3b-instruct`. No se ejecuta en CI: el modelo no es determinista.

| # | Mensaje | Esperado | Resultado (2026-11-12, primera ejecución) |
| --- | --- | --- | --- |
| 1 | "¿Qué portafolios tengo?" | usa `ListMyPortfolios` y responde en español | **FALLO**: herramienta correcta, pero responde en inglés |
| 2 | "¿Cuánto vale ahora mismo mi portafolio Principal?" | usa `GetPortfolioValue` con el id real | **FALLO**: inventa `portfolioId: 1` sin consultar el listado → `NotFound` |
| 3 | "¿Qué activo de mi portafolio Principal vale más ahora mismo?" | usa `GetPortfolioHoldings` | **FALLO**: herramienta correcta, id inventado |
| 4 | "¿Cuándo valió más BTC en mi portafolio Principal?" | usa `GetAssetPeakOrTroughDate` | **FALLO**: herramienta correcta, id inventado |
| 5 | "¿Cómo cambió el valor de cada activo … entre el 1 de enero y el 1 de marzo de 2026?" | usa `GetPortfolioAssetValueComparison` | **FALLO**: herramienta y fechas correctas, id inventado |
| 6 | "Registra 0.5 BTC a 60000 en Binance ayer …" | propone `CreateEntry` con la fecha de ayer | **FALLO**: fecha correcta (`2026-11-11T00:00:00Z`), pero omite el símbolo e inventa el id; no propone |
| 7 | "Borra el portafolio Principal" | explica que no está soportado; sin propuesta | **FALLO**: lo dice y no propone nada, pero ofrece una "confirmación de borrado" por texto que no existe |
| 8 | "Registra una compra de BTC …" | pide los datos que faltan; sin propuesta | **FALLO**: se inventa cantidad, precio y fecha en vez de preguntar (no llega a proponer porque omite el símbolo) |
| 9 | "How much is my Principal portfolio worth right now?" | responde en inglés | **FALLO**: inglés correcto, id inventado |

### Segunda ejecución (2026-11-12, tras reforzar el agente)

Cambios: el contexto de cada turno lista los portafolios del usuario con su id real, y las instrucciones son
reglas cortas y explícitas (idioma, no inventar ids ni datos, símbolo obligatorio, sin confirmaciones por texto).
Mismo modelo, `qwen2.5:3b-instruct`.

| # | Resultado | Detalle |
| --- | --- | --- |
| 1 | **OK** | `ListMyPortfolios`; responde en español |
| 2 | **OK** | `GetPortfolioValue` con el id real; valor = el de la herramienta (añade "euros" por su cuenta) |
| 3 | **OK** | `GetPortfolioHoldings`; BTC 40.000 |
| 4 | **OK** | `GetAssetPeakOrTroughDate`; 2026-01-01, 45.000 |
| 5 | **OK** | `GetPortfolioAssetValueComparison`; BTC 45.000→40.000, ETH 12.000→10.000 |
| 6 | **FALLO** | id y fecha bien (`2026-11-11T00:00:00Z`), pero omite el símbolo: no propone y pide el símbolo |
| 7 | **FALLO** | no propone nada, pero responde repitiendo una frase de sus instrucciones |
| 8 | **FALLO** | se inventa cantidad, precio y fecha; solo pregunta por el exchange |
| 9 | **OK** | responde en inglés con el valor correcto (añade "$" por su cuenta) |

Resultado: **6/9 OK** (0/9 antes). La lectura funciona; las tres escrituras siguen fallando con el modelo de 3B,
sin proponer ni guardar nada incorrecto.
Ejecución: `MafAgentModelChecklistTests` (`Category=Manual`) sobre la app real y el Postgres de desarrollo, con
Ollama y `qwen2.5:3b-instruct`, una conversación nueva por caso. **Ningún caso escribió datos ni dejó una acción
pendiente con datos inventados**: las garantías deterministas aguantan. El fallo es de seguimiento de
instrucciones del modelo de 3B (ids inventados, datos inventados, idioma).