"use client";

import { useState } from "react";
import { X } from "lucide-react";
import { daysSince } from "@/lib/format";
import type { PortfolioHoldingItemResponse } from "@/types";

const STALE_THRESHOLD_DAYS = 30;

interface AlertsStripProps {
  holdings: PortfolioHoldingItemResponse[];
}

// Operational alerts only, computed entirely from the user's own data — no price
// thresholds, no external feed. Quiet and dismissible, never a modal.
export function AlertsStrip({ holdings }: AlertsStripProps) {
  const [dismissed, setDismissed] = useState(false);

  const stale = holdings.filter((h) => daysSince(h.recordedAt) >= STALE_THRESHOLD_DAYS);

  if (dismissed || stale.length === 0) {
    return null;
  }

  return (
    <div
      role="status"
      className="flex items-start justify-between gap-4 rounded-xl bg-paper-raised px-4 py-3 text-sm"
    >
      <div>
        <p className="font-medium">Avisos</p>
        <ul className="mt-1 space-y-0.5 text-ink-muted">
          {stale.map((h) => (
            <li key={`${h.cryptoCurrencyId}-${h.exchangeId}`}>
              {h.cryptoCurrencySymbol} en {h.exchangeName}: sin registrar hace{" "}
              {daysSince(h.recordedAt)} días.
            </li>
          ))}
        </ul>
      </div>
      <button
        type="button"
        onClick={() => setDismissed(true)}
        aria-label="Descartar avisos"
        className="shrink-0 text-ink-muted hover:text-ink"
      >
        <X size={16} strokeWidth={1.75} aria-hidden />
      </button>
    </div>
  );
}
