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
import { colorForKey } from "@/lib/chartColors";
import type { PortfolioAssetHistorySeriesResponse, PortfolioHistoryPointResponse } from "@/types";

type Range = "30d" | "90d" | "1y" | "all";

const RANGE_OPTIONS: { value: Range; label: string }[] = [
  { value: "30d", label: "30d" },
  { value: "90d", label: "90d" },
  { value: "1y", label: "1a" },
  { value: "all", label: "Todo" },
];

const RANGE_DAYS: Record<Range, number | null> = { "30d": 30, "90d": 90, "1y": 365, all: null };

interface MergedPoint {
  date: string;
  value: number;
  byAsset: Record<string, number>;
}

interface ValueHistoryChartProps {
  points: PortfolioHistoryPointResponse[];
  byAsset: PortfolioAssetHistorySeriesResponse[];
  // undefined means "still loading" — must render as "—", never fall back to 0 and
  // read as a real (and wrong) answer.
  currentValue: number | undefined;
}

export function ValueHistoryChart({ points, byAsset, currentValue }: ValueHistoryChartProps) {
  const [range, setRange] = useState<Range>("90d");
  const [hovered, setHovered] = useState<MergedPoint | null>(null);

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

  // Multiple lines only earn their place once there's more than one asset to tell
  // apart — a single asset's line is identical to the total and would just double it.
  const showAssetLines = byAsset.length > 1;

  const merged = useMemo<MergedPoint[]>(() => {
    if (!showAssetLines) return filtered.map((p) => ({ date: p.date, value: p.value, byAsset: {} }));
    const byDate = byAsset.map((series) => ({
      symbol: series.symbol,
      values: new Map(series.points.map((p) => [p.date, p.value])),
    }));
    return filtered.map((p) => ({
      date: p.date,
      value: p.value,
      byAsset: Object.fromEntries(byDate.map((s) => [s.symbol, s.values.get(p.date) ?? 0])),
    }));
  }, [filtered, byAsset, showAssetLines]);

  const reading = hovered ?? merged[merged.length - 1] ?? null;
  const delta = merged.length >= 2 ? merged[merged.length - 1].value - merged[0].value : null;

  const legend = useMemo(
    () =>
      showAssetLines
        ? [...byAsset]
            .map((series) => ({ symbol: series.symbol, color: colorForKey(series.symbol) }))
            .sort((a, b) => a.symbol.localeCompare(b.symbol))
        : [],
    [byAsset, showAssetLines]
  );

  return (
    <section className="border border-rule bg-paper p-4" aria-label="Histórico de valor">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="text-xs font-medium tracking-wide text-ink-muted uppercase">Histórico</h2>
          <p className="tabular text-xl font-semibold">
            {reading
              ? formatCurrency(reading.value)
              : currentValue !== undefined
                ? formatCurrency(currentValue)
                : "—"}
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
        {merged.length > 1 ? (
          <ResponsiveContainer width="100%" height="100%">
            <LineChart
              data={merged}
              onMouseMove={(state: unknown) => {
                const activePayload = (state as { activePayload?: { payload: MergedPoint }[] })
                  ?.activePayload;
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
              <Tooltip
                cursor={{ stroke: "var(--ink)", strokeWidth: 1 }}
                content={({ active, payload }) => {
                  if (!active || !payload?.length) return null;
                  const row = payload[0].payload as MergedPoint;
                  const breakdown = legend
                    .map((entry) => ({ ...entry, value: row.byAsset[entry.symbol] ?? 0 }))
                    .filter((entry) => entry.value > 0)
                    .sort((a, b) => b.value - a.value);
                  // Nothing to break down for a single-asset portfolio (no `legend`) —
                  // the header reading above already covers the total.
                  if (breakdown.length === 0) return null;
                  return (
                    <div className="border border-rule bg-paper px-2.5 py-2 text-xs">
                      <p className="mb-1 text-ink-muted">{formatDate(row.date)}</p>
                      <ul className="space-y-0.5">
                        {breakdown.map((entry) => (
                          <li key={entry.symbol} className="flex items-center gap-2">
                            <span
                              aria-hidden
                              className="h-0.5 w-2.5 shrink-0"
                              style={{ background: entry.color }}
                            />
                            <span className="flex-1">{entry.symbol}</span>
                            <span className="tabular">{formatCurrency(entry.value)}</span>
                          </li>
                        ))}
                      </ul>
                    </div>
                  );
                }}
              />
              <Line
                type="monotone"
                dataKey="value"
                stroke="var(--ink)"
                strokeWidth={1.5}
                dot={false}
                activeDot={{ r: 3, fill: "var(--ink)", stroke: "none" }}
                isAnimationActive={false}
              />
              {legend.map((entry) => (
                <Line
                  key={entry.symbol}
                  type="monotone"
                  name={entry.symbol}
                  dataKey={(row: MergedPoint) => row.byAsset[entry.symbol]}
                  stroke={entry.color}
                  strokeWidth={1}
                  dot={false}
                  activeDot={{ r: 2.5, fill: entry.color, stroke: "none" }}
                  isAnimationActive={false}
                />
              ))}
            </LineChart>
          </ResponsiveContainer>
        ) : (
          <p className="flex h-full items-center justify-center text-center text-sm text-ink-muted">
            Registra entradas en más de una fecha para ver el histórico.
          </p>
        )}
      </div>

      {legend.length > 0 && (
        <ul className="mt-3 flex flex-wrap gap-x-3 gap-y-1.5 border-t border-rule pt-2.5 text-xs text-ink-muted">
          <li className="flex items-center gap-1.5">
            <span aria-hidden className="h-0.5 w-2.5 shrink-0 bg-ink" />
            Total
          </li>
          {legend.map((entry) => (
            <li key={entry.symbol} className="flex items-center gap-1.5">
              <span aria-hidden className="h-0.5 w-2.5 shrink-0" style={{ background: entry.color }} />
              {entry.symbol}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
