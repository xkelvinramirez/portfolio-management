import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { type PortfolioEntryListParams, portfolioEntryService } from "@/services/api";
import type { CreatePortfolioEntryRequest, UpdatePortfolioEntryRequest } from "@/types";
import { portfolioKeys } from "./usePortfolios";

export const portfolioEntryKeys = {
  all: ["portfolioEntries"] as const,
  lists: () => [...portfolioEntryKeys.all, "list"] as const,
  list: (params: PortfolioEntryListParams = {}) => [...portfolioEntryKeys.lists(), params] as const,
  details: () => [...portfolioEntryKeys.all, "detail"] as const,
  detail: (id: number) => [...portfolioEntryKeys.details(), id] as const,
};

export function usePortfolioEntries(params: PortfolioEntryListParams = {}) {
  return useQuery({
    queryKey: portfolioEntryKeys.list(params),
    queryFn: () => portfolioEntryService.list(params),
  });
}

export function usePortfolioEntry(id: number) {
  return useQuery({
    queryKey: portfolioEntryKeys.detail(id),
    queryFn: () => portfolioEntryService.getById(id),
    enabled: id > 0,
  });
}

/**
 * Invalidates everything a written entry can change: the raw entry lists, and every
 * derived view for its portfolio (value, history, holdings, allocation all read from
 * PortfolioEntry rows server-side).
 */
function invalidatePortfolioEntryEffects(
  queryClient: ReturnType<typeof useQueryClient>,
  portfolioId: number
) {
  queryClient.invalidateQueries({ queryKey: portfolioEntryKeys.lists() });
  queryClient.invalidateQueries({ queryKey: portfolioKeys.detail(portfolioId) });
}

export function useCreatePortfolioEntry() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreatePortfolioEntryRequest) => portfolioEntryService.create(data),
    onSuccess: (_result, variables) => {
      invalidatePortfolioEntryEffects(queryClient, variables.portfolioId);
    },
  });
}

export function useUpdatePortfolioEntry(id: number, portfolioId: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: UpdatePortfolioEntryRequest) => portfolioEntryService.update(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: portfolioEntryKeys.detail(id) });
      invalidatePortfolioEntryEffects(queryClient, portfolioId);
    },
  });
}

export function useDeletePortfolioEntry(portfolioId: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: number) => portfolioEntryService.remove(id),
    onSuccess: () => {
      invalidatePortfolioEntryEffects(queryClient, portfolioId);
    },
  });
}
