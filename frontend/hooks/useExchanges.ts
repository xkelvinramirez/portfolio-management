import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { exchangeService, type ListParams } from "@/services/api";
import type { CreateExchangeRequest, UpdateExchangeRequest } from "@/types";

export const exchangeKeys = {
  all: ["exchanges"] as const,
  lists: () => [...exchangeKeys.all, "list"] as const,
  list: (params: ListParams = {}) => [...exchangeKeys.lists(), params] as const,
  details: () => [...exchangeKeys.all, "detail"] as const,
  detail: (id: number) => [...exchangeKeys.details(), id] as const,
};

export function useExchanges(params: ListParams = {}) {
  return useQuery({
    queryKey: exchangeKeys.list(params),
    queryFn: () => exchangeService.list(params),
  });
}

export function useExchange(id: number) {
  return useQuery({
    queryKey: exchangeKeys.detail(id),
    queryFn: () => exchangeService.getById(id),
    enabled: id > 0,
  });
}

export function useCreateExchange() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateExchangeRequest) => exchangeService.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: exchangeKeys.lists() });
    },
  });
}

export function useUpdateExchange(id: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: UpdateExchangeRequest) => exchangeService.update(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: exchangeKeys.detail(id) });
      queryClient.invalidateQueries({ queryKey: exchangeKeys.lists() });
    },
  });
}

export function useDeleteExchange() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: number) => exchangeService.remove(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: exchangeKeys.lists() });
    },
  });
}
