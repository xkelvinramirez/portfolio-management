import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { PendingAction } from "@/types";

// Where a proposed write stands from the user's side. "pending" waits for a click;
// the three result states come from the confirm endpoint; "rejected" from reject;
// "unavailable" when the action could not be claimed (expired, already decided, gone).
export type ActionState = "pending" | "Succeeded" | "Failed" | "Stale" | "rejected" | "unavailable";

export interface ProposedAction {
  action: PendingAction;
  state: ActionState;
  // Spanish text shown under the card for Failed / Stale / unavailable.
  note?: string;
}

export interface MafChatTurn {
  // "notice" is a line from the panel itself (e.g. the conversation expired), not from the agent.
  role: "user" | "agent" | "notice";
  content: string;
  actions?: ProposedAction[];
}

// One server-side MAF conversation. conversationId is null until the first reply, and again after it expires.
export interface MafThread {
  conversationId: string | null;
  history: MafChatTurn[];
}

export const EMPTY_THREAD: MafThread = { conversationId: null, history: [] };

interface MafAgentChatState {
  // One thread per portfolio, keyed by portfolio id: switching portfolio in the dashboard switches
  // conversation, so the agent never carries one portfolio's context into another.
  threads: Record<number, MafThread>;
  setConversationId: (portfolioId: number, id: string | null) => void;
  addTurn: (portfolioId: number, turn: MafChatTurn) => void;
  updateAction: (portfolioId: number, actionId: string, patch: Pick<ProposedAction, "state" | "note">) => void;
  clear: (portfolioId: number) => void;

  hasHydrated: boolean;
  setHasHydrated: (value: boolean) => void;
}

function patchThread(
  threads: Record<number, MafThread>,
  portfolioId: number,
  patch: (thread: MafThread) => MafThread
): Record<number, MafThread> {
  return { ...threads, [portfolioId]: patch(threads[portfolioId] ?? EMPTY_THREAD) };
}

// Separate from useAgentChatStore, which belongs to the SK agent on /agent.
// The model's memory is on the server; this copy only redraws each thread after a reload.
export const useMafAgentChatStore = create<MafAgentChatState>()(
  persist(
    (set) => ({
      threads: {},
      setConversationId: (portfolioId, id) =>
        set((state) => ({
          threads: patchThread(state.threads, portfolioId, (t) => ({ ...t, conversationId: id })),
        })),
      addTurn: (portfolioId, turn) =>
        set((state) => ({
          threads: patchThread(state.threads, portfolioId, (t) => ({ ...t, history: [...t.history, turn] })),
        })),
      updateAction: (portfolioId, actionId, patch) =>
        set((state) => ({
          threads: patchThread(state.threads, portfolioId, (t) => ({
            ...t,
            history: t.history.map((turn) =>
              turn.actions?.some((a) => a.action.id === actionId)
                ? {
                    ...turn,
                    actions: turn.actions.map((a) => (a.action.id === actionId ? { ...a, ...patch } : a)),
                  }
                : turn
            ),
          })),
        })),
      clear: (portfolioId) =>
        set((state) => {
          const threads = { ...state.threads };
          delete threads[portfolioId];
          return { threads };
        }),

      hasHydrated: false,
      setHasHydrated: (value) => set({ hasHydrated: value }),
    }),
    {
      name: "maf-agent-chat",
      partialize: (state) => ({ threads: state.threads }),
      onRehydrateStorage: () => (state) => {
        state?.setHasHydrated(true);
      },
    }
  )
);
