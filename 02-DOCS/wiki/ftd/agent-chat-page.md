---
author: kramirez
branch: agent-chat-page (fusionada en development con el PR #16; rama y worktree retirados el 2026-11-12)
status: merged — prueba E2E 2026-11-12: el frontend cumple, la conversación falla por el agente SK (ver Evidence)
---
# Página de chat con el agente

## Intent

Conversar en lenguaje natural con el agente de portafolios ya existente (`POST /api/v1/Agent/chat`,
JWT, request `{ message }`, response `{ reply }`) desde una página nueva en el frontend Next.js, con
sensación de chat continuo.

## Scope

**In:**
- Ruta `/agent`, entrada de nav "Agente" (ícono `Bot`)
- Historial de turnos visible (burbujas usuario/agente), persistido en `localStorage`
- Recorte de historial 100% client-side al armar el `message` saliente (sin tocar el backend)
- Estados de carga/error/timeout, botón "Nueva conversación"

**Out (explícitamente no):**
- Cambios al contrato o al backend (`AgentOrchestrator` sigue sin memoria server-side)
- Multi-conversación (listas de hilos guardados) — un solo hilo activo
- Streaming de tokens

**Anti-goal:** el recorte de historial no debe romper el límite de 2000 caracteres del `Message` en
el servidor sin que el usuario se entere — el recorte es silencioso hacia el agente, pero el usuario
sigue viendo su historial completo en pantalla.

**Constraints observadas:** `Message` del backend ≤2000 chars (`ValidationBehavior`); sin streaming;
respuestas pueden tardar bastante (Ollama local en CPU, `RequestTimeoutSeconds=240s` en dev).

## Checklist

- [x] Tipos `SendAgentMessageRequest`/`SendAgentMessageResponse` en `frontend/types/index.ts` —
      prueba: compila y coincide con `src/Contracts/Agent/*.cs`
- [x] `agentService.chat(message)` en `frontend/services/api.ts` — prueba: llama `POST /Agent/chat`
- [x] `useAgentChat` (mutation React Query) en `frontend/hooks/useAgentChat.ts` — prueba: expone
      `sendMessage`/`isPending`/`error`
- [x] Store `frontend/store/useAgentChatStore.ts` (zustand + `persist`, patrón de
      `useAppStore`/`hasHydrated` — evita el lint `react-hooks/set-state-in-effect` que un
      `useState`+`useEffect` manual disparaba) — prueba: compila, persiste en `localStorage`
- [x] Página `frontend/app/(app)/agent/page.tsx`: hilo leído del store, función de recorte
      (~1800 chars, turnos más viejos primero), burbujas usuario/agente, textarea+enviar, loader,
      error legible, botón "Nueva conversación" — prueba: ruta compila, prerenderiza y responde
      `200` en dev; **no verificado**: conversación real end-to-end (requiere login + Ollama local
      corriendo, no disponible en esta sesión)
- [x] Nav: entrada "Agente" en `frontend/components/layout/navItems.ts` — prueba: aparece en el
      build de rutas (`/agent` listada) junto al resto del sidebar
- [x] `npm run lint` sin errores nuevos
- [x] `npm run build` sin errores nuevos

## Evidence

- `npm run lint`: `✓` sin salida de errores (tras corregir un `react-hooks/set-state-in-effect`
  moviendo el historial a un store zustand con `persist`, igual que `useAppStore`).
- `npm run build`: compiló en 13.3s, TypeScript en 17.9s, `/agent` listada como ruta estática (`○`)
  en el resumen de rutas.
- `npm run dev` + `curl http://localhost:3000/agent`: `HTTP 200`, el HTML devuelto contiene
  "Cargando" (el `LoadingScreen` de `AuthGuard` sin token) y ningún error de hidratación/excepción
  en el log del servidor.
### Prueba E2E con modelo real — 2026-11-12

Contra la API real (`development`, Postgres de desarrollo) con Ollama `qwen2.5:7b-instruct`. Un usuario
desechable con el portafolio "Principal" (1 BTC a 40.000 y 5 ETH a 2.000). Cada mensaje se armó con la misma
lógica que `buildOutgoingMessage` (presupuesto de 1800 caracteres, turnos recientes primero) y se envió a
`POST /api/v1/Agent/chat`, como hace la página. Al final se borraron los usuarios de prueba (en cascada).

| Caso | Enviado | Resultado |
| --- | --- | --- |
| 1. "¿Cuánto vale ahora mismo mi portafolio Principal?" | 49 caracteres | HTTP 200 en 65 s, `reply` **vacío** |
| 2. Seguimiento con historial | 152 caracteres | HTTP 200 en 10 s, `reply` **vacío** |
| 3. Historial largo (24 turnos) | 1759 caracteres: el recorte funciona y no pasa de 2000 | HTTP 200 en 75 s, `reply` **vacío** |
| 4. Timeout (`Ai__RequestTimeoutSeconds=2`) | 49 caracteres | HTTP 400 `Agent.Timeout` en 5 s; la página mostraría su `description` |

- **Frontend: cumple.** El recorte respeta el límite, la petición tiene el contrato correcto y
  `getApiErrorMessage` extrae la `description` del timeout.
- **Conversación: falla, por el backend.** El agente SK nunca llama a `ListMyPortfolios`. Se inventa
  `portfolioId=1`, recibe `null` y repite `GetPortfolioValue`/`GetPortfolioHoldings` hasta que
  `LoopCappingFilter` corta, en los tres casos. La API devuelve 200 con `reply` vacío, y la página pinta una
  burbuja del agente vacía. Es el mismo fallo que tuvo el agente MAF antes de su refuerzo (ids en el contexto
  del turno). El agente MAF ya lo resuelve.
- **Detalles menores de la página:**
  - una respuesta vacía no se trata como error;
  - el texto del timeout llega en inglés ("Agent execution halted: …").
- **Hallazgo aparte:** `POST /api/v1/Portfolio` devuelve `id: 0`. La respuesta se arma antes de que
  `UnitOfWorkBehavior` guarde.
- No verificado: la apariencia visual de las burbujas, porque esta sesión no tiene navegador.

## Next

La página está fusionada y cumple su alcance. Para que la conversación funcione hay que decidir:
1. Apuntar `/agent` al agente MAF (`/api/v1/MafAgent/chat`, que guarda la conversación en el servidor y
   propone acciones).
2. O reforzar el agente SK como se hizo con MAF. Esto toca `*/Agent/` y necesita tu aprobación.

Mejoras pequeñas de la página, independientes de lo anterior: tratar un `reply` vacío como error y traducir
`Agent.Timeout`.
