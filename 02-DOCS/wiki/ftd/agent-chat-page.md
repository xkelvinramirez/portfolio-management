---
author: kramirez
branch: agent-chat-page
status: done
merged: 2026-11-06
pr: #17
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
- Pendiente para quien continúe: probar la conversación real contra el backend con Ollama local
  levantado (`docker compose up -d` + login), y confirmar visualmente las burbujas, el recorte de
  historial largo, y el estado de error ante un timeout real del agente.

## Next
Fusionado en master el 2026-11-06 (4e08b1a).
