"use client";

import { useMemo, useState } from "react";
import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { SegmentedControl } from "@/components/ui/SegmentedControl";
import { formatCurrency, formatDate, formatShortDate } from "@/lib/format";
import type { PortfolioHistoryPointResponse } from "@/types";

type Range = "30d" | "90d" | "1y" | "all";

const RANGE_OPTIONS: { value: Range; label: string }[] = [
  { value: "30d", label: "30d" },
  { value: "90d", label: "90d" },
  { value: "1y", label: "1a" },
  { value: "all", label: "Todo" },
];

const RANGE_DAYS: Record<Range, number | null> = { "30d": 30, "90d": 90, "1y": 365, all: null };

interface ValueHistoryChartProps {
  points: PortfolioHistoryPointResponse[];
  currentValue: number;
}

export function ValueHistoryChart({ points, currentValue }: ValueHistoryChartProps) {
  const [range, setRange] = useState<Range>("90d");
  const [hovered, setHovered] = useState<PortfolioHistoryPointResponse | null>(null);

  // Date.now() is impure and must not run directly during render (it would also bake
  // a build-time value into the prerendered HTML and mismatch on hydration); a lazy
  // useState initializer is the sanctioned one-time-impure-read pattern instead.
  const [now] = useState(() => Date.now());

  const filtered = useMemo(() => {
    const days = RANGE_DAYS[range];
    if (days === null) return points;
    const cutoff = now - days * 24 * 60 * 60 * 1000;
    return points.filter((p) => new Date(p.date).getTime() >= cutoff);
  }, [points, range, now]);

  const reading = hovered ?? filtered[filtered.length - 1] ?? null;
  const delta =
    filtered.length >= 2 ? filtered[filtered.length - 1].value - filtered[0].value : null;

  return (
    <section className="border border-rule bg-paper p-4" aria-label="Histórico de valor">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="text-xs font-medium tracking-wide text-ink-muted uppercase">Histórico</h2>
          <p className="tabular text-xl font-semibold">
            {formatCurrency(reading ? reading.value : currentValue)}
          </p>
          <div className="flex items-center gap-2 text-xs text-ink-muted">
            <span>{reading ? formatDate(reading.date) : "Sin historial todavía"}</span>
            {delta !== null && !hovered && (
              <span className={`tabular ${delta < 0 ? "text-accent" : "text-ink"}`}>
                {delta >= 0 ? "+" : ""}
                {formatCurrency(delta)}
              </span>
            )}
          </div>
        </div>
        <SegmentedControl
          aria-label="Rango del histórico"
          options={RANGE_OPTIONS}
          value={range}
          onChange={setRange}
        />
      </div>

      <div className="mt-4 h-48">
        {filtered.length > 1 ? (
          <ResponsiveContainer width="100%" height="100%">
            <LineChart
              data={filtered}
              onMouseMove={(state: unknown) => {
                const activePayload = (
                  state as { activePayload?: { payload: PortfolioHistoryPointResponse }[] }
                )?.activePayload;
                setHovered(activePayload?.[0]?.payload ?? null);
              }}
              onMouseLeave={() => setHovered(null)}
              margin={{ top: 4, right: 4, left: 0, bottom: 0 }}
            >
              <CartesianGrid stroke="var(--rule)" vertical={false} />
              <XAxis
                dataKey="date"
                tickFormatter={(value: string) => formatShortDate(value)}
                stroke="var(--ink-muted)"
                fontSize={11}
                tickLine={false}
                axisLine={{ stroke: "var(--rule)" }}
                minTickGap={24}
              />
              <YAxis hide domain={["auto", "auto"]} />
              <Tooltip content={() => null} cursor={{ stroke: "var(--ink)", strokeWidth: 1 }} />
              <Line
                type="monotone"
                dataKey="value"
                stroke="var(--ink)"
                strokeWidth={1.5}
                dot={false}
                activeDot={{ r: 3, fill: "var(--ink)", stroke: "none" }}
                isAnimationActive={false}
              />
            </LineChart>
          </ResponsiveContainer>
        ) : (
          <p className="flex h-full items-center justify-center text-center text-sm text-ink-muted">
            Registra entradas en más de una fecha para ver el histórico.
          </p>
        )}
      </div>
    </section>
  );
}
