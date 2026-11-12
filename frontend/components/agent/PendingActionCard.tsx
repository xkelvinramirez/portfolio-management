import { Check, Clock, X } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { fieldLabel, formatFieldValue, OPERATION_LABELS } from "@/lib/mafAgent";
import type { ProposedAction } from "@/store/useMafAgentChatStore";

interface PendingActionCardProps {
  proposed: ProposedAction;
  busy: boolean;
  onConfirm: () => void;
  onReject: () => void;
}

const STATE_LABELS: Record<Exclude<ProposedAction["state"], "pending">, string> = {
  Succeeded: "Hecho",
  Failed: "No se pudo aplicar",
  Stale: "Desfasada",
  rejected: "Rechazada",
  unavailable: "No disponible",
};

// One write the agent proposed. Nothing runs until the user clicks Confirmar here — the chat
// text can never confirm it. For edits the previous value is shown next to the new one.
export function PendingActionCard({ proposed, busy, onConfirm, onReject }: PendingActionCardProps) {
  const { action, state, note } = proposed;
  const fields = Object.entries(action.values);
  const expiresAt = new Date(action.expiresAt).toLocaleTimeString("es-ES", {
    hour: "2-digit",
    minute: "2-digit",
  });

  return (
    <div className="rounded-lg border border-rule bg-paper px-3.5 py-3 text-sm">
      <div className="flex items-center justify-between gap-3">
        <p className="font-semibold text-ink">{OPERATION_LABELS[action.operation] ?? action.operation}</p>
        {state === "pending" ? (
          <span className="flex items-center gap-1 text-xs text-ink-muted">
            <Clock size={12} strokeWidth={1.75} aria-hidden />
            Caduca a las {expiresAt}
          </span>
        ) : (
          <span
            className={
              "text-xs font-semibold " + (state === "Succeeded" ? "text-brand" : "text-ink-muted")
            }
          >
            {STATE_LABELS[state]}
          </span>
        )}
      </div>

      {fields.length > 0 && (
        <dl className="mt-2 grid grid-cols-[auto_1fr] gap-x-3 gap-y-1">
          {fields.map(([key, value]) => {
            const previous = action.before?.[key];
            const changed = action.before !== null && previous !== value;
            return (
              <div key={key} className="contents">
                <dt className="text-ink-muted">{fieldLabel(key)}</dt>
                <dd className="break-words text-ink">
                  {changed && (
                    <span className="text-ink-muted line-through">{formatFieldValue(previous ?? null)}</span>
                  )}
                  {changed && " → "}
                  {formatFieldValue(value)}
                </dd>
              </div>
            );
          })}
        </dl>
      )}

      {note && <p className="mt-2 text-xs text-ink-muted">{note}</p>}

      {state === "pending" && (
        <div className="mt-3 flex justify-end gap-2">
          <Button variant="ghost" onClick={onReject} disabled={busy}>
            <span className="flex items-center gap-1">
              <X size={14} strokeWidth={1.75} aria-hidden />
              Rechazar
            </span>
          </Button>
          <Button onClick={onConfirm} disabled={busy}>
            <span className="flex items-center gap-1">
              <Check size={14} strokeWidth={1.75} aria-hidden />
              {busy ? "Aplicando…" : "Confirmar"}
            </span>
          </Button>
        </div>
      )}
    </div>
  );
}
