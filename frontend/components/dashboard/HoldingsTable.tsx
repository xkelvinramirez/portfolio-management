"use client";

import { useMemo, useState } from "react";
import { ArrowUpDown } from "lucide-react";
import { formatCurrency, formatDate, formatQuantity } from "@/lib/format";
import type { PortfolioHoldingItemResponse } from "@/types";

type SortKey = "asset" | "exchange" | "value";
type SortDirection = "asc" | "desc";

const SORT_COLUMNS: { key: SortKey; label: string }[] = [
  { key: "asset", label: "Activo" },
  { key: "exchange", label: "Exchange" },
  { key: "value", label: "Valor" },
];

interface HoldingsTableProps {
  holdings: PortfolioHoldingItemResponse[];
}

// The protagonist of the surface: a full-width ruled table, not a card grid.
export function HoldingsTable({ holdings }: HoldingsTableProps) {
  const [query, setQuery] = useState("");
  const [sortKey, setSortKey] = useState<SortKey>("value");
  const [sortDirection, setSortDirection] = useState<SortDirection>("desc");

  const rows = useMemo(() => {
    const q = query.trim().toLowerCase();
    const filtered = q
      ? holdings.filter(
          (h) =>
            h.cryptoCurrencySymbol.toLowerCase().includes(q) ||
            h.cryptoCurrencyName.toLowerCase().includes(q) ||
            h.exchangeName.toLowerCase().includes(q)
        )
      : holdings;

    return [...filtered].sort((a, b) => {
      let comparison = 0;
      if (sortKey === "asset") {
        comparison = a.cryptoCurrencySymbol.localeCompare(b.cryptoCurrencySymbol);
      } else if (sortKey === "exchange") {
        comparison = a.exchangeName.localeCompare(b.exchangeName);
      } else {
        comparison = a.value - b.value;
      }
      return sortDirection === "asc" ? comparison : -comparison;
    });
  }, [holdings, query, sortKey, sortDirection]);

  function toggleSort(key: SortKey) {
    if (key === sortKey) {
      setSortDirection((d) => (d === "asc" ? "desc" : "asc"));
    } else {
      setSortKey(key);
      setSortDirection("desc");
    }
  }

  return (
    <section aria-label="Holdings actuales" className="border border-rule bg-paper">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-rule px-4 py-3">
        <h2 className="text-sm font-medium">Holdings</h2>
        <input
          type="search"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Filtrar por activo o exchange…"
          className="w-full max-w-64 border border-rule bg-paper px-2.5 py-1.5 text-sm placeholder:text-ink-muted focus-visible:outline-2 focus-visible:outline-ink"
        />
      </div>

      {holdings.length === 0 ? (
        <p className="px-4 py-10 text-center text-sm text-ink-muted">
          Todavía no hay holdings registrados en este portfolio.
        </p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[640px] border-collapse text-sm">
            <thead>
              <tr className="border-b border-rule-strong text-left text-xs tracking-wide text-ink-muted uppercase">
                {SORT_COLUMNS.map((col) => (
                  <th key={col.key} scope="col" className="px-4 py-2 font-medium">
                    <button
                      type="button"
                      onClick={() => toggleSort(col.key)}
                      className="inline-flex items-center gap-1 hover:text-ink"
                      aria-label={`Ordenar por ${col.label}`}
                    >
                      {col.label}
                      <ArrowUpDown
                        size={12}
                        strokeWidth={1.75}
                        aria-hidden
                        className={sortKey === col.key ? "opacity-100" : "opacity-30"}
                      />
                    </button>
                  </th>
                ))}
                <th scope="col" className="px-4 py-2 text-right font-medium">
                  Cantidad
                </th>
                <th scope="col" className="px-4 py-2 text-right font-medium">
                  Precio
                </th>
                <th scope="col" className="px-4 py-2 text-right font-medium">
                  Registrado
                </th>
              </tr>
            </thead>
            <tbody>
              {rows.length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-4 py-8 text-center text-ink-muted">
                    Ningún activo coincide con &ldquo;{query}&rdquo;.
                  </td>
                </tr>
              ) : (
                rows.map((h) => (
                  <tr
                    key={`${h.cryptoCurrencyId}-${h.exchangeId}`}
                    className="border-b border-rule last:border-0 hover:bg-paper-raised"
                  >
                    <td className="px-4 py-2.5">
                      <span className="font-medium">{h.cryptoCurrencySymbol}</span>{" "}
                      <span className="text-ink-muted">{h.cryptoCurrencyName}</span>
                    </td>
                    <td className="px-4 py-2.5 text-ink-muted">{h.exchangeName}</td>
                    <td className="tabular px-4 py-2.5 text-right">{formatCurrency(h.value)}</td>
                    <td className="tabular px-4 py-2.5 text-right text-ink-muted">
                      {formatQuantity(h.quantity)}
                    </td>
                    <td className="tabular px-4 py-2.5 text-right text-ink-muted">
                      {formatCurrency(h.pricePerUnit)}
                    </td>
                    <td className="px-4 py-2.5 text-right text-ink-muted">
                      {formatDate(h.recordedAt)}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
