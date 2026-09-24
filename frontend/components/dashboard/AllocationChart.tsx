"use client";

import { useState } from "react";
import { Cell, Pie, PieChart, ResponsiveContainer, Tooltip } from "recharts";
import { ErrorNotice } from "@/components/ui/ErrorNotice";
import { SegmentedControl } from "@/components/ui/SegmentedControl";
import { usePortfolioAllocation } from "@/hooks/usePortfolioAnalytics";
import { PortfolioAllocationGroupBy } from "@/types";
import { formatCurrency, formatPercentage } from "@/lib/format";
import { getApiErrorMessage } from "@/lib/errors";

type GroupBy = "asset" | "exchange";

const GROUP_OPTIONS: { value: GroupBy; label: string }[] = [
  { value: "asset", label: "Activo" },
  { value: "exchange", label: "Exchange" },
];

// One hue family at varying value, not a rainbow — the chart reads as one ledger,
// consistent with the single-accent discipline (red stays reserved for negatives).
const SLICE_COLORS = ["#211d17", "#4a4336", "#6f6555", "#948a75", "#b9ae95", "#ddd3ba"];

interface AllocationChartProps {
  portfolioId: number;
}

export function AllocationChart({ portfolioId }: AllocationChartProps) {
  const [groupBy, setGroupBy] = useState<GroupBy>("asset");
  const { data, isPending, isError, error } = usePortfolioAllocation(
    portfolioId,
    groupBy === "asset" ? PortfolioAllocationGroupBy.Asset : PortfolioAllocationGroupBy.Exchange
  );

  const items = data?.items ?? [];

  return (
    <section className="border border-rule bg-paper p-4" aria-label="Participación por activo o exchange">
      <div className="flex items-start justify-between gap-3">
        <h2 className="text-xs font-medium tracking-wide text-ink-muted uppercase">
          Participación
        </h2>
        <SegmentedControl
          aria-label="Agrupar participación por"
          options={GROUP_OPTIONS}
          value={groupBy}
          onChange={setGroupBy}
        />
      </div>

      {isPending ? (
        <div className="mt-4 h-48 animate-pulse bg-paper-raised" />
      ) : isError ? (
        <div className="mt-4">
          <ErrorNotice>{getApiErrorMessage(error) ?? "No se pudo cargar la participación."}</ErrorNotice>
        </div>
      ) : items.length === 0 ? (
        <p className="mt-4 flex h-48 items-center justify-center text-center text-sm text-ink-muted">
          Sin holdings todavía.
        </p>
      ) : (
        <div className="mt-2 flex items-center gap-4">
          <div className="h-40 w-40 shrink-0">
            <ResponsiveContainer width="100%" height="100%">
              <PieChart>
                <Pie
                  data={items}
                  dataKey="value"
                  nameKey="label"
                  innerRadius={48}
                  outerRadius={70}
                  paddingAngle={1}
                  stroke="var(--paper)"
                  strokeWidth={2}
                  isAnimationActive={false}
                >
                  {items.map((item, index) => (
                    <Cell key={item.groupId} fill={SLICE_COLORS[index % SLICE_COLORS.length]} />
                  ))}
                </Pie>
                <Tooltip
                  formatter={(value) => formatCurrency(Number(value))}
                  contentStyle={{
                    background: "var(--paper)",
                    border: "1px solid var(--rule)",
                    borderRadius: 0,
                    fontSize: 12,
                  }}
                />
              </PieChart>
            </ResponsiveContainer>
          </div>
          <ul className="min-w-0 flex-1 space-y-1.5 text-sm">
            {items.map((item, index) => (
              <li key={item.groupId} className="flex items-center justify-between gap-2">
                <span className="flex min-w-0 items-center gap-1.5">
                  <span
                    aria-hidden
                    className="size-2 shrink-0 rounded-full"
                    style={{ background: SLICE_COLORS[index % SLICE_COLORS.length] }}
                  />
                  <span className="truncate">{item.label}</span>
                </span>
                <span className="tabular shrink-0 text-ink-muted">
                  {formatPercentage(item.percentage)}
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}
