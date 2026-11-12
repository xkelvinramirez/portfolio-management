import { isAxiosError } from "axios";
import type { PendingActionOperation } from "@/types";

// Spanish labels for the MAF agent's pending actions. The backend's own `summary` is a technical
// English line meant for logs, so the card is built from these instead.

export const OPERATION_LABELS: Record<PendingActionOperation, string> = {
  CreatePortfolio: "Crear portafolio",
  UpdatePortfolio: "Editar portafolio",
  CreateEntry: "Registrar entrada",
  UpdateEntry: "Editar entrada",
  CreateNote: "Crear nota",
  UpdateNote: "Editar nota",
  RefreshPrices: "Actualizar precios",
  SendStatusReport: "Enviar informe de estado",
};

// Keys arrive as the request's property names; matched case-insensitively so either casing works.
const FIELD_LABELS: Record<string, string> = {
  id: "Id",
  portfolioid: "Portafolio (id)",
  entryid: "Entrada (id)",
  noteid: "Nota (id)",
  cryptocurrencyid: "Criptomoneda (id)",
  exchangeid: "Exchange (id)",
  quantity: "Cantidad",
  priceperunit: "Precio por unidad",
  recordedat: "Fecha",
  name: "Nombre",
  description: "Descripción",
  category: "Categoría",
  title: "Título",
  content: "Contenido",
  isactive: "Activa",
  createdat: "Creada",
  periodstart: "Inicio del periodo",
  periodend: "Fin del periodo",
};

export function fieldLabel(key: string): string {
  return FIELD_LABELS[key.toLowerCase()] ?? key;
}

const ISO_UTC = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?Z$/;

// A date the user gave without a time is stored as that day at 00:00 UTC (project decision),
// so it is shown as just that day; any other instant in the viewer's local time.
const UTC_MIDNIGHT = /T00:00(:00(\.0+)?)?Z$/;

// Dates travel as ISO-8601 UTC; show them in the viewer's local time, the rest as is.
export function formatFieldValue(value: string | null): string {
  if (value === null || value === "") return "—";
  const lower = value.toLowerCase();
  if (lower === "true") return "Sí";
  if (lower === "false") return "No";
  if (ISO_UTC.test(value)) {
    const date = new Date(value);
    return UTC_MIDNIGHT.test(value)
      ? date.toLocaleDateString("es-ES", { dateStyle: "medium", timeZone: "UTC" })
      : date.toLocaleString("es-ES", { dateStyle: "medium", timeStyle: "short" });
  }
  return value;
}

const ERROR_MESSAGES: Record<string, string> = {
  "MafAgent.Conversation.NotFound": "Esta conversación ya no existe. Tu próximo mensaje empezará una nueva.",
  "MafAgent.Conversation.Expired":
    "La conversación caducó tras 24 horas sin actividad. Tu próximo mensaje empezará una nueva.",
  "MafAgent.Portfolio.NotFound": "Este portafolio ya no existe. Recarga el dashboard y elige otro.",
  "MafAgent.PendingAction.NotFound": "Esta acción ya no existe.",
  "MafAgent.PendingAction.Expired": "La acción caducó. Pídele al agente que la proponga de nuevo.",
  "MafAgent.PendingAction.AlreadyDecided": "Esta acción ya se confirmó o se rechazó.",
  "MafAgent.LoopExhausted": "El agente necesitó demasiados pasos. Prueba con una petición más simple.",
  "MafAgent.Timeout": "El agente tardó demasiado en responder. Inténtalo de nuevo.",
};

export const CONVERSATION_GONE_CODES = new Set([
  "MafAgent.Conversation.NotFound",
  "MafAgent.Conversation.Expired",
]);

// First ErrorOr code of a failed request, if the backend sent one.
export function getApiErrorCode(error: unknown): string | null {
  if (isAxiosError(error)) {
    const data: unknown = error.response?.data;
    if (Array.isArray(data) && typeof data[0]?.code === "string") {
      return data[0].code;
    }
  }
  return null;
}

// Spanish text for a MAF agent error; null when the code is unknown (the caller falls back).
export function getMafAgentErrorMessage(error: unknown): string | null {
  const code = getApiErrorCode(error);
  return code ? (ERROR_MESSAGES[code] ?? null) : null;
}
