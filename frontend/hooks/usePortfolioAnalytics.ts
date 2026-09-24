import { useQuery } from "@tanstack/react-query";
import { portfolioService } from "@/services/api";
import type { PortfolioAllocationGroupBy } from "@/types";
import { portfolioKeys } from "./usePortfolios";

export function usePortfolioValue(id: number, date?: string) {
  return useQuery({
    queryKey: [...portfolioKeys.detail(id), "value", date] as const,
    queryFn: () => portfolioService.getValue(id, date),
    enabled: id > 0,
  });
}

export function usePortfolioHistory(id: number) {
  return useQuery({
    queryKey: [...portfolioKeys.detail(id), "history"] as const,
    queryFn: () => portfolioService.getHistory(id),
    enabled: id > 0,
  });
}

export function usePortfolioHoldings(id: number, date?: string) {
  return useQuery({
    queryKey: [...portfolioKeys.detail(id), "holdings", date] as const,
    queryFn: () => portfolioService.getHoldings(id, date),
    enabled: id > 0,
  });
}

export function usePortfolioAllocation(
  id: number,
  groupBy: PortfolioAllocationGroupBy,
  date?: string
) {
  return useQuery({
    queryKey: [...portfolioKeys.detail(id), "allocation", groupBy, date] as const,
    queryFn: () => portfolioService.getAllocation(id, groupBy, date),
    enabled: id > 0,
  });
}
