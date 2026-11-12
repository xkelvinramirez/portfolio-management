"use client";

import { type FormEvent, useEffect, useRef, useState } from "react";
import { Bot, Info, Send, User, X } from "lucide-react";
import { PendingActionCard } from "@/components/agent/PendingActionCard";
import { Button } from "@/components/ui/Button";
import { ErrorNotice } from "@/components/ui/ErrorNotice";
import { IconButton } from "@/components/ui/IconButton";
import { useConfirmPendingAction, useMafAgentChat, useRejectPendingAction } from "@/hooks/useMafAgentChat";
import { getApiErrorMessage } from "@/lib/errors";
import { CONVERSATION_GONE_CODES, getApiErrorCode, getMafAgentErrorMessage } from "@/lib/mafAgent";
import { EMPTY_THREAD, useMafAgentChatStore } from "@/store/useMafAgentChatStore";

// The MAF agent keeps the conversation on the server (24 h without activity), so only the
// new message travels; the server caps it at 2000 chars.
const MAX_MESSAGE_CHARS = 2000;

const EMPTY_REPLY = "El agente no devolvió ninguna respuesta. Prueba a reformular la pregunta.";

const STALE_NOTE =
  "Los datos cambiaron desde que el agente la propuso, así que no se aplicó. Pídesela de nuevo.";

interface MafAgentPanelProps {
  open: boolean;
  onClose: () => void;
  portfolioId: number;
  portfolioName: string;
}

// Side panel over the dashboard for the MAF agent. Every message carries the portfolio the
// dashboard shows, and each portfolio has its own conversation, so the agent always knows which
// one "it" refers to. It stays mounted while closed: a reply that arrives later is not lost.
export function MafAgentPanel({ open, onClose, portfolioId, portfolioName }: MafAgentPanelProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const scrollRef = useRef<HTMLDivElement>(null);

  const hasHydrated = useMafAgentChatStore((state) => state.hasHydrated);
  const thread = useMafAgentChatStore((state) => state.threads[portfolioId]) ?? EMPTY_THREAD;
  const setConversationId = useMafAgentChatStore((state) => state.setConversationId);
  const addTurn = useMafAgentChatStore((state) => state.addTurn);
  const updateAction = useMafAgentChatStore((state) => state.updateAction);
  const clear = useMafAgentChatStore((state) => state.clear);

  const [draft, setDraft] = useState("");
  const [busyActionId, setBusyActionId] = useState<string | null>(null);
  // The portfolio the in-flight (or last failed) message was sent for: the "thinking" bubble and
  // the error belong to that thread, not to whichever portfolio is selected now.
  const [sentFor, setSentFor] = useState<number | null>(null);
  const { sendMessage, isPending, error, reset } = useMafAgentChat();
  const confirmAction = useConfirmPendingAction();
  const rejectAction = useRejectPendingAction();

  const pendingHere = isPending && sentFor === portfolioId;
  const errorHere = error !== null && sentFor === portfolioId ? error : null;

  // The native <dialog> in modal mode gives Esc to close, a focus trap and an inert page behind.
  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;
    if (open && !dialog.open) dialog.showModal();
    if (!open && dialog.open) dialog.close();
  }, [open]);

  useEffect(() => {
    if (open) scrollRef.current?.scrollIntoView({ block: "end" });
  }, [open, thread.history, pendingHere, portfolioId]);

  async function handleSend(e: FormEvent) {
    e.preventDefault();
    const question = draft.trim();
    if (!question || isPending) return;

    // Captured now: the user may switch portfolio while the agent is thinking.
    const targetPortfolioId = portfolioId;
    const conversationId = thread.conversationId;
    addTurn(targetPortfolioId, { role: "user", content: question });
    setDraft("");
    setSentFor(targetPortfolioId);

    try {
      const response = await sendMessage({ conversationId, message: question, portfolioId: targetPortfolioId });
      setConversationId(targetPortfolioId, response.conversationId);
      const actions = response.pendingActions.map((action) => ({ action, state: "pending" as const }));
      const reply = response.reply.trim();
      addTurn(targetPortfolioId, {
        role: "agent",
        content: reply || (actions.length > 0 ? "" : EMPTY_REPLY),
        actions,
      });
    } catch (err) {
      // An expired or unknown conversation is not a failure the user can retry as is:
      // forget it, say so in the thread, and let the next message start a new one.
      const code = getApiErrorCode(err);
      if (code && CONVERSATION_GONE_CODES.has(code)) {
        setConversationId(targetPortfolioId, null);
        addTurn(targetPortfolioId, { role: "notice", content: getMafAgentErrorMessage(err) ?? "" });
        reset();
      }
      // Any other error is shown via `error`; the user's message stays in the thread.
    }
  }

  async function handleConfirm(actionId: string) {
    const targetPortfolioId = portfolioId;
    setBusyActionId(actionId);
    try {
      const result = await confirmAction.mutateAsync(actionId);
      if (result.status === "Succeeded") {
        updateAction(targetPortfolioId, actionId, { state: "Succeeded", note: undefined });
      } else if (result.status === "Stale") {
        updateAction(targetPortfolioId, actionId, { state: "Stale", note: STALE_NOTE });
      } else {
        updateAction(targetPortfolioId, actionId, {
          state: "Failed",
          note: result.error?.description ?? "El servidor no pudo aplicar el cambio.",
        });
      }
    } catch (err) {
      updateAction(targetPortfolioId, actionId, unclaimed(err));
    } finally {
      setBusyActionId(null);
    }
  }

  async function handleReject(actionId: string) {
    const targetPortfolioId = portfolioId;
    setBusyActionId(actionId);
    try {
      await rejectAction.mutateAsync(actionId);
      updateAction(targetPortfolioId, actionId, { state: "rejected", note: undefined });
    } catch (err) {
      updateAction(targetPortfolioId, actionId, unclaimed(err));
    } finally {
      setBusyActionId(null);
    }
  }

  function handleNewConversation() {
    clear(portfolioId);
    if (sentFor === portfolioId) reset();
  }

  return (
    <dialog
      ref={dialogRef}
      aria-labelledby="maf-agent-panel-title"
      // Esc and the X close the dialog natively; onClose keeps the parent's state in sync.
      onClose={onClose}
      // A click on the backdrop lands on the <dialog> itself, never on its content.
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
      className="fixed inset-y-0 right-0 left-auto m-0 h-dvh max-h-dvh w-full max-w-full bg-transparent p-0 backdrop:bg-black/40 sm:max-w-[440px]"
    >
      <div className="flex h-full flex-col border-l border-rule bg-paper text-ink shadow-xl">
        <header className="flex items-start justify-between gap-3 border-b border-rule px-4 py-3">
          <div className="min-w-0">
            <h2 id="maf-agent-panel-title" className="flex items-center gap-1.5 text-base font-bold">
              <Bot size={18} strokeWidth={1.75} aria-hidden />
              Asistente
            </h2>
            <p className="truncate text-xs text-ink-muted">
              Portafolio: <span className="font-semibold text-ink">{portfolioName}</span>
            </p>
          </div>
          <div className="flex shrink-0 items-center gap-2">
            <Button
              variant="ghost"
              onClick={handleNewConversation}
              disabled={thread.history.length === 0 || pendingHere}
            >
              Nueva
            </Button>
            <IconButton aria-label="Cerrar asistente" onClick={onClose}>
              <X size={18} strokeWidth={1.75} aria-hidden />
            </IconButton>
          </div>
        </header>

        <div className="min-h-0 flex-1 overflow-y-auto px-4 py-4">
          {!hasHydrated ? null : thread.history.length === 0 && !pendingHere ? (
            <p className="py-8 text-center text-sm text-ink-muted">
              Pregunta por <span className="font-semibold">{portfolioName}</span> o pídele que registre un
              cambio. Nada se guarda hasta que pulses Confirmar.
            </p>
          ) : (
            <ul className="flex flex-col gap-3">
              {thread.history.map((turn, index) =>
                turn.role === "notice" ? (
                  <li key={index} className="flex items-center justify-center gap-1.5 text-xs text-ink-muted">
                    <Info size={14} strokeWidth={1.75} aria-hidden />
                    {turn.content}
                  </li>
                ) : (
                  <li
                    key={index}
                    className={"flex gap-2 " + (turn.role === "user" ? "justify-end" : "justify-start")}
                  >
                    {turn.role === "agent" && (
                      <Bot size={18} strokeWidth={1.75} className="mt-1.5 shrink-0 text-ink-muted" aria-hidden />
                    )}
                    <div className="flex max-w-[85%] flex-col gap-2">
                      {turn.content && (
                        <div
                          className={
                            "rounded-lg px-3.5 py-2 text-sm whitespace-pre-wrap " +
                            (turn.role === "user" ? "bg-brand text-white" : "bg-paper-raised text-ink")
                          }
                        >
                          {turn.content}
                        </div>
                      )}
                      {turn.actions?.map((proposed) => (
                        <PendingActionCard
                          key={proposed.action.id}
                          proposed={proposed}
                          busy={busyActionId === proposed.action.id}
                          onConfirm={() => handleConfirm(proposed.action.id)}
                          onReject={() => handleReject(proposed.action.id)}
                        />
                      ))}
                    </div>
                    {turn.role === "user" && (
                      <User size={18} strokeWidth={1.75} className="mt-1.5 shrink-0 text-ink-muted" aria-hidden />
                    )}
                  </li>
                )
              )}
              {pendingHere && (
                <li className="flex justify-start gap-2">
                  <Bot size={18} strokeWidth={1.75} className="mt-1.5 shrink-0 text-ink-muted" aria-hidden />
                  <div className="max-w-[85%] animate-pulse rounded-lg bg-paper-raised px-3.5 py-2 text-sm text-ink-muted">
                    Pensando… puede tardar, el modelo corre en local.
                  </div>
                </li>
              )}
            </ul>
          )}
          <div ref={scrollRef} />
        </div>

        {errorHere && (
          <div className="border-t border-rule px-4 py-2">
            <ErrorNotice>
              {getMafAgentErrorMessage(errorHere) ??
                getApiErrorMessage(errorHere) ??
                "No se pudo contactar al agente. Intenta de nuevo."}
            </ErrorNotice>
          </div>
        )}

        {isPending && !pendingHere && (
          <p className="border-t border-rule px-4 py-2 text-xs text-ink-muted">
            El agente está respondiendo en otro portafolio. Espera a que termine.
          </p>
        )}

        <form onSubmit={handleSend} className="flex items-end gap-2 border-t border-rule px-4 py-3">
          <textarea
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter" && !e.shiftKey) {
                e.preventDefault();
                handleSend(e);
              }
            }}
            aria-label={`Mensaje al asistente sobre ${portfolioName}`}
            placeholder={`Pregunta sobre ${portfolioName}…`}
            rows={2}
            maxLength={MAX_MESSAGE_CHARS}
            className="flex-1 resize-none rounded-lg border border-rule bg-paper px-3 py-2 text-sm text-ink placeholder:text-ink-muted focus-visible:outline-2 focus-visible:outline-brand"
          />
          <Button type="submit" disabled={isPending || !draft.trim()} aria-label="Enviar">
            <Send size={14} strokeWidth={1.75} aria-hidden />
          </Button>
        </form>
      </div>
    </dialog>
  );
}

// The action could not be claimed: expired, already decided or gone (4xx), or the request
// itself failed. Only the first group closes the card; a network error leaves it pending.
function unclaimed(err: unknown): { state: "unavailable" | "pending"; note: string } {
  const message = getMafAgentErrorMessage(err);
  if (message && getApiErrorCode(err)?.startsWith("MafAgent.PendingAction.")) {
    return { state: "unavailable", note: message };
  }
  return { state: "pending", note: "No se pudo completar. Inténtalo de nuevo." };
}
