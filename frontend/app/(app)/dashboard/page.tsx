"use client";

import { useEffect, useMemo } from "react";
import { usePortfolios } from "@/hooks/usePortfolios";
import {
  usePortfolioHistory,
  usePortfolioHoldings,
  usePortfolioValue,
} from "@/hooks/usePortfolioAnalytics";
import { useAppStore } from "@/store/useAppStore";
import { AlertsStrip } from "@/components/dashboard/AlertsStrip";
import { AllocationChart } from "@/components/dashboard/AllocationChart";
import { HoldingsTable } from "@/components/dashboard/HoldingsTable";
import { ValueHistoryChart } from "@/components/dashboard/ValueHistoryChart";
import { formatCurrency } from "@/lib/format";

export default function DashboardPage() {
  const { data: portfoliosPage, isPending: portfoliosPending } = usePortfolios({ limit: 50 });
  const selectedPortfolioId = useAppStore((state) => state.selectedPortfolioId);
  const setSelectedPortfolioId = useAppStore((state) => state.setSelectedPortfolioId);

  const portfolios = useMemo(() => portfoliosPage?.data ?? [], [portfoliosPage]);

  useEffect(() => {
    if (!selectedPortfolioId && portfolios.length > 0) {
      setSelectedPortfolioId(portfolios[0].id);
    }
  }, [selectedPortfolioId, portfolios, setSelectedPortfolioId]);

  const activePortfolioId = selectedPortfolioId ?? portfolios[0]?.id ?? 0;
  const activePortfolio = portfolios.find((p) => p.id === activePortfolioId);

  const { data: value } = usePortfolioValue(activePortfolioId);
  const { data: history } = usePortfolioHistory(activePortfolioId);
  const { data: holdingsData, isPending: holdingsPending } = usePortfolioHoldings(
    activePortfolioId
  );

  const holdings = holdingsData?.holdings ?? [];

  if (portfoliosPending) {
    return <DashboardSkeleton />;
  }

  if (portfolios.length === 0) {
    return <EmptyPortfolioState />;
  }

  return (
    <div className="mx-auto flex max-w-6xl flex-col gap-6">
      <div>
        {portfolios.length > 1 && (
          <label className="mb-1 block text-xs text-ink-muted">
            Portfolio
            <select
              value={activePortfolioId}
              onChange={(e) => setSelectedPortfolioId(Number(e.target.value))}
              className="ml-2 border border-rule bg-paper px-1.5 py-0.5 text-ink"
            >
              {portfolios.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>
          </label>
        )}
        <h1 className="text-xl font-semibold tracking-tight">
          {activePortfolio?.name ?? "Portfolio"}
        </h1>
        <p className="tabular text-3xl font-semibold">
          {value ? formatCurrency(value.value) : "—"}
        </p>
      </div>

      <AlertsStrip holdings={holdings} />

      <div className="grid gap-6 md:grid-cols-2">
        <ValueHistoryChart points={history?.points ?? []} currentValue={value?.value ?? 0} />
        <AllocationChart portfolioId={activePortfolioId} />
      </div>

      {holdingsPending ? (
        <div className="h-40 animate-pulse border border-rule bg-paper-raised" />
      ) : (
        <HoldingsTable holdings={holdings} />
      )}
    </div>
  );
}

function DashboardSkeleton() {
  return (
    <div className="mx-auto flex max-w-6xl animate-pulse flex-col gap-6" aria-busy="true">
      <div className="h-16 w-64 bg-paper-raised" />
      <div className="grid gap-6 md:grid-cols-2">
        <div className="h-56 border border-rule bg-paper-raised" />
        <div className="h-56 border border-rule bg-paper-raised" />
      </div>
      <div className="h-64 border border-rule bg-paper-raised" />
    </div>
  );
}

function EmptyPortfolioState() {
  return (
    <div className="mx-auto flex max-w-md flex-col items-center gap-2 py-24 text-center">
      <h1 className="text-lg font-semibold">Todavía no tienes un portfolio</h1>
      <p className="text-sm text-ink-muted">
        Crea tu primer portfolio para empezar a registrar holdings y ver tu ledger aquí.
      </p>
    </div>
  );
}
