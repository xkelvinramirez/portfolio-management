import { useMutation, useQueryClient } from "@tanstack/react-query";
import { mafAgentService } from "@/services/api";
import type { SendMafAgentMessageRequest } from "@/types";
import { noteKeys } from "./useNotes";
import { portfolioEntryKeys } from "./usePortfolioEntries";
import { portfolioKeys } from "./usePortfolios";

export function useMafAgentChat() {
  const mutation = useMutation({
    mutationFn: (data: SendMafAgentMessageRequest) => mafAgentService.chat(data),
  });

  return {
    sendMessage: mutation.mutateAsync,
    isPending: mutation.isPending,
    error: mutation.error,
    reset: mutation.reset,
  };
}

// A confirmed action can create or edit portfolios, entries or notes (or refresh prices),
// so every list and detail that could show it is refetched — whatever the outcome, since a
// Failed or Stale result also means the user should see the current data. This also redraws
// the dashboard behind the panel (holdings and history live under portfolioKeys).
export function useConfirmPendingAction() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => mafAgentService.confirm(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: portfolioKeys.all });
      queryClient.invalidateQueries({ queryKey: portfolioEntryKeys.all });
      queryClient.invalidateQueries({ queryKey: noteKeys.all });
    },
  });
}

export function useRejectPendingAction() {
  return useMutation({
    mutationFn: (id: string) => mafAgentService.reject(id),
  });
}
