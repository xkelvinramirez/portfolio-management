import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { type PortfolioListParams, portfolioService } from "@/services/api";
import type { CreatePortfolioRequest, UpdatePortfolioRequest } from "@/types";

export const portfolioKeys = {
  all: ["portfolios"] as const,
  lists: () => [...portfolioKeys.all, "list"] as const,
  list: (params: PortfolioListParams = {}) => [...portfolioKeys.lists(), params] as const,
  details: () => [...portfolioKeys.all, "detail"] as const,
  detail: (id: number) => [...portfolioKeys.details(), id] as const,
};

export function usePortfolios(params: PortfolioListParams = {}) {
  return useQuery({
    queryKey: portfolioKeys.list(params),
    queryFn: () => portfolioService.list(params),
  });
}

export function usePortfolio(id: number) {
  return useQuery({
    queryKey: portfolioKeys.detail(id),
    queryFn: () => portfolioService.getById(id),
    enabled: id > 0,
  });
}

export function useCreatePortfolio() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreatePortfolioRequest) => portfolioService.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: portfolioKeys.lists() });
    },
  });
}

export function useUpdatePortfolio(id: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: UpdatePortfolioRequest) => portfolioService.update(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: portfolioKeys.detail(id) });
      queryClient.invalidateQueries({ queryKey: portfolioKeys.lists() });
    },
  });
}

export function useDeletePortfolio() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: number) => portfolioService.remove(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: portfolioKeys.lists() });
    },
  });
}
