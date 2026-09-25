"use client";

import { useEffect, useMemo } from "react";
import { usePortfolios } from "@/hooks/usePortfolios";
import { usePortfolioHistory, usePortfolioHoldings } from "@/hooks/usePortfolioAnalytics";
import Link from "next/link";
import { useAppStore } from "@/store/useAppStore";
import { AlertsStrip } from "@/components/dashboard/AlertsStrip";
import { AllocationChart } from "@/components/dashboard/AllocationChart";
import { buttonClassName } from "@/components/ui/Button";
import { ErrorNotice } from "@/components/ui/ErrorNotice";
import { HoldingsTable } from "@/components/dashboard/HoldingsTable";
import { ValueHistoryChart } from "@/components/dashboard/ValueHistoryChart";
import { formatCurrency } from "@/lib/format";
import { getApiErrorMessage } from "@/lib/errors";

export default function DashboardPage() {
  const {
    data: portfoliosPage,
    isPending: portfoliosPending,
    isError: portfoliosError,
    error: portfoliosErrorDetail,
  } = usePortfolios({ limit: 50 });
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

  const { data: history, isError: historyError, error: historyErrorDetail } =
    usePortfolioHistory(activePortfolioId);
  const {
    data: holdingsData,
    isPending: holdingsPending,
    isError: holdingsError,
    error: holdingsErrorDetail,
  } = usePortfolioHoldings(activePortfolioId);

  const holdings = holdingsData?.holdings ?? [];
  // No live feed and no price movement between entries (product principle: manual
  // entry is the source of truth) — the latest history point IS the current value,
  // so there's no need for a second endpoint call to fetch it separately.
  const currentValue = history?.points.at(-1)?.value;

  if (portfoliosPending) {
    return <DashboardSkeleton />;
  }

  if (portfoliosError) {
    return (
      <ErrorNotice>
        {getApiErrorMessage(portfoliosErrorDetail) ?? "No se pudieron cargar tus portfolios."}
      </ErrorNotice>
    );
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
              className="ml-2 rounded-lg border border-rule bg-paper px-2 py-1 text-ink"
            >
              {portfolios.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>
          </label>
        )}
        <h1 className="text-2xl font-bold tracking-tight">
          {activePortfolio?.name ?? "Portfolio"}
        </h1>
        <p className="tabular text-4xl font-extrabold">
          {currentValue !== undefined ? formatCurrency(currentValue) : "—"}
        </p>
      </div>

      <AlertsStrip holdings={holdings} />

      <div className="grid gap-6 md:grid-cols-[7fr_3fr]">
        {historyError ? (
          <div className="panel p-1">
            <ErrorNotice>
              {getApiErrorMessage(historyErrorDetail) ?? "No se pudo cargar el histórico."}
            </ErrorNotice>
          </div>
        ) : (
          <ValueHistoryChart
            points={history?.points ?? []}
            byAsset={history?.byAsset ?? []}
            currentValue={currentValue}
          />
        )}
        <AllocationChart portfolioId={activePortfolioId} />
      </div>

      {holdingsPending ? (
        <div className="h-40 animate-pulse rounded-2xl bg-paper-raised" />
      ) : holdingsError ? (
        <div className="panel p-1">
          <ErrorNotice>
            {getApiErrorMessage(holdingsErrorDetail) ?? "No se pudieron cargar los holdings."}
          </ErrorNotice>
        </div>
      ) : (
        <HoldingsTable holdings={holdings} />
      )}
    </div>
  );
}

function DashboardSkeleton() {
  return (
    <div className="mx-auto flex max-w-6xl animate-pulse flex-col gap-6" aria-busy="true">
      <div className="h-16 w-64 rounded-2xl bg-paper-raised" />
      <div className="grid gap-6 md:grid-cols-[7fr_3fr]">
        <div className="h-56 rounded-2xl bg-paper-raised" />
        <div className="h-56 rounded-2xl bg-paper-raised" />
      </div>
      <div className="h-64 rounded-2xl bg-paper-raised" />
    </div>
  );
}

function EmptyPortfolioState() {
  return (
    <div className="mx-auto flex max-w-md flex-col items-center gap-3 py-24 text-center">
      <h1 className="text-xl font-bold">Todavía no tienes un portfolio</h1>
      <p className="text-sm text-ink-muted">
        Crea tu primer portfolio para empezar a registrar holdings y ver tu ledger aquí.
      </p>
      <Link href="/portfolio" className={buttonClassName() + " mt-1"}>
        Crear portfolio
      </Link>
    </div>
  );
}
