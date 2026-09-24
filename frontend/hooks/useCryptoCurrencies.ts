import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { cryptoCurrencyService, type ListParams } from "@/services/api";
import type { CreateCryptoCurrencyRequest, UpdateCryptoCurrencyRequest } from "@/types";

export const cryptoCurrencyKeys = {
  all: ["cryptoCurrencies"] as const,
  lists: () => [...cryptoCurrencyKeys.all, "list"] as const,
  list: (params: ListParams = {}) => [...cryptoCurrencyKeys.lists(), params] as const,
  details: () => [...cryptoCurrencyKeys.all, "detail"] as const,
  detail: (id: number) => [...cryptoCurrencyKeys.details(), id] as const,
};

export function useCryptoCurrencies(params: ListParams = {}) {
  return useQuery({
    queryKey: cryptoCurrencyKeys.list(params),
    queryFn: () => cryptoCurrencyService.list(params),
  });
}

export function useCryptoCurrency(id: number) {
  return useQuery({
    queryKey: cryptoCurrencyKeys.detail(id),
    queryFn: () => cryptoCurrencyService.getById(id),
    enabled: id > 0,
  });
}

export function useCreateCryptoCurrency() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateCryptoCurrencyRequest) => cryptoCurrencyService.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: cryptoCurrencyKeys.lists() });
    },
  });
}

export function useUpdateCryptoCurrency(id: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: UpdateCryptoCurrencyRequest) => cryptoCurrencyService.update(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: cryptoCurrencyKeys.detail(id) });
      queryClient.invalidateQueries({ queryKey: cryptoCurrencyKeys.lists() });
    },
  });
}

export function useDeleteCryptoCurrency() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: number) => cryptoCurrencyService.remove(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: cryptoCurrencyKeys.lists() });
    },
  });
}
