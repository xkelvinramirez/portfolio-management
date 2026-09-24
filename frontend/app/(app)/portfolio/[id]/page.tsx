"use client";

import { type FormEvent, use, useMemo, useState } from "react";
import Link from "next/link";
import { Check, X } from "lucide-react";
import { useCryptoCurrencies } from "@/hooks/useCryptoCurrencies";
import { useExchanges } from "@/hooks/useExchanges";
import { usePortfolio } from "@/hooks/usePortfolios";
import {
  useCreatePortfolioEntry,
  useDeletePortfolioEntry,
  usePortfolioEntries,
  useUpdatePortfolioEntry,
} from "@/hooks/usePortfolioEntries";
import { Button } from "@/components/ui/Button";
import { ErrorNotice } from "@/components/ui/ErrorNotice";
import { IconButton } from "@/components/ui/IconButton";
import { RowActions } from "@/components/ui/RowActions";
import { SelectField } from "@/components/ui/SelectField";
import { TextField } from "@/components/ui/TextField";
import { formatCurrency, formatDate, formatQuantity } from "@/lib/format";
import { getApiErrorMessage } from "@/lib/errors";
import type { CryptoCurrencyResponse, ExchangeResponse, PortfolioEntryResponse } from "@/types";

function todayInputValue() {
  return new Date().toISOString().slice(0, 10);
}

export default function PortfolioDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = use(params);
  const portfolioId = Number(id);

  const { data: portfolio, isError: portfolioError, error: portfolioErrorDetail } =
    usePortfolio(portfolioId);
  const {
    data: entriesPage,
    isPending: entriesPending,
    isError: entriesError,
    error: entriesErrorDetail,
  } = usePortfolioEntries({ portfolioId, limit: 200 });
  const { data: cryptoPage, isError: cryptoError } = useCryptoCurrencies({ limit: 200 });
  const { data: exchangePage, isError: exchangeError } = useExchanges({ limit: 200 });

  const cryptoCurrencies = useMemo(() => cryptoPage?.data ?? [], [cryptoPage]);
  const exchanges = useMemo(() => exchangePage?.data ?? [], [exchangePage]);
  const entries = useMemo(
    () => [...(entriesPage?.data ?? [])].sort((a, b) => b.recordedAt.localeCompare(a.recordedAt)),
    [entriesPage]
  );

  const cryptoById = useMemo(
    () => new Map(cryptoCurrencies.map((c) => [c.id, c])),
    [cryptoCurrencies]
  );
  const exchangeById = useMemo(() => new Map(exchanges.map((e) => [e.id, e])), [exchanges]);

  const createMutation = useCreatePortfolioEntry();
  const deleteMutation = useDeletePortfolioEntry(portfolioId);

  const [editingId, setEditingId] = useState<number | null>(null);
  const [cryptoCurrencyId, setCryptoCurrencyId] = useState("");
  const [exchangeId, setExchangeId] = useState("");
  const [quantity, setQuantity] = useState("");
  const [pricePerUnit, setPricePerUnit] = useState("");
  const [recordedAt, setRecordedAt] = useState(todayInputValue());

  const referenceDataFailed = cryptoError || exchangeError;
  const canAddEntry = !referenceDataFailed && cryptoCurrencies.length > 0 && exchanges.length > 0;

  function handleCreate(e: FormEvent) {
    e.preventDefault();
    if (!cryptoCurrencyId || !exchangeId || !quantity || !pricePerUnit) return;
    createMutation.mutate(
      {
        portfolioId,
        cryptoCurrencyId: Number(cryptoCurrencyId),
        exchangeId: Number(exchangeId),
        quantity: Number(quantity),
        pricePerUnit: Number(pricePerUnit),
        recordedAt: new Date(recordedAt).toISOString(),
      },
      {
        onSuccess: () => {
          setQuantity("");
          setPricePerUnit("");
        },
      }
    );
  }

  if (portfolioError) {
    return (
      <div className="mx-auto flex max-w-4xl flex-col gap-3">
        <Link href="/portfolio" className="text-xs text-ink-muted hover:underline">
          ← Portfolios
        </Link>
        <ErrorNotice>
          {getApiErrorMessage(portfolioErrorDetail) ?? "No se pudo cargar este portfolio."}
        </ErrorNotice>
      </div>
    );
  }

  return (
    <div className="mx-auto flex max-w-4xl flex-col gap-6">
      <div>
        <Link href="/portfolio" className="text-xs text-ink-muted hover:underline">
          ← Portfolios
        </Link>
        <h1 className="text-xl font-semibold tracking-tight">{portfolio?.name ?? "Portfolio"}</h1>
        {portfolio?.description && (
          <p className="text-sm text-ink-muted">{portfolio.description}</p>
        )}
      </div>

      <section className="border border-rule bg-paper">
        {entriesPending ? (
          <div className="h-32 animate-pulse bg-paper-raised" />
        ) : entriesError ? (
          <ErrorNotice>
            {getApiErrorMessage(entriesErrorDetail) ?? "No se pudieron cargar las entradas."}
          </ErrorNotice>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[720px] border-collapse text-sm">
              <thead>
                <tr className="border-b border-rule-strong text-left text-xs tracking-wide text-ink-muted uppercase">
                  <th className="px-4 py-2 font-medium">Activo</th>
                  <th className="px-4 py-2 font-medium">Exchange</th>
                  <th className="px-4 py-2 text-right font-medium">Cantidad</th>
                  <th className="px-4 py-2 text-right font-medium">Precio</th>
                  <th className="px-4 py-2 text-right font-medium">Valor</th>
                  <th className="px-4 py-2 font-medium">Fecha</th>
                  <th className="px-4 py-2 text-right font-medium">Acciones</th>
                </tr>
              </thead>
              <tbody>
                {entries.length === 0 && (
                  <tr>
                    <td colSpan={7} className="px-4 py-8 text-center text-ink-muted">
                      Todavía no hay entradas en este portfolio.
                    </td>
                  </tr>
                )}
                {entries.map((entry) =>
                  editingId === entry.id ? (
                    <EditEntryRow
                      key={entry.id}
                      entry={entry}
                      portfolioId={portfolioId}
                      cryptoCurrencies={cryptoCurrencies}
                      exchanges={exchanges}
                      onDone={() => setEditingId(null)}
                    />
                  ) : (
                    <tr
                      key={entry.id}
                      className="border-b border-rule last:border-0 hover:bg-paper-raised"
                    >
                      <td className="px-4 py-2.5 font-medium">
                        {cryptoById.get(entry.cryptoCurrencyId)?.symbol ??
                          `#${entry.cryptoCurrencyId}`}
                      </td>
                      <td className="px-4 py-2.5 text-ink-muted">
                        {exchangeById.get(entry.exchangeId)?.name ?? `#${entry.exchangeId}`}
                      </td>
                      <td className="tabular px-4 py-2.5 text-right text-ink-muted">
                        {formatQuantity(entry.quantity)}
                      </td>
                      <td className="tabular px-4 py-2.5 text-right text-ink-muted">
                        {formatCurrency(entry.pricePerUnit)}
                      </td>
                      <td className="tabular px-4 py-2.5 text-right">
                        {formatCurrency(entry.quantity * entry.pricePerUnit)}
                      </td>
                      <td className="px-4 py-2.5 text-ink-muted">
                        {formatDate(entry.recordedAt)}
                      </td>
                      <td className="px-4 py-2.5 text-right">
                        <RowActions
                          onEdit={() => setEditingId(entry.id)}
                          onDelete={() => deleteMutation.mutate(entry.id)}
                          deleting={
                            deleteMutation.isPending && deleteMutation.variables === entry.id
                          }
                        />
                      </td>
                    </tr>
                  )
                )}
              </tbody>
            </table>
          </div>
        )}

        {canAddEntry ? (
          <form
            onSubmit={handleCreate}
            className="flex flex-wrap items-end gap-3 border-t border-rule px-4 py-3"
          >
            <SelectField
              label="Activo"
              name="cryptoCurrencyId"
              value={cryptoCurrencyId}
              onChange={(e) => setCryptoCurrencyId(e.target.value)}
              required
              className="w-28"
            >
              <option value="" disabled>
                —
              </option>
              {cryptoCurrencies.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.symbol}
                </option>
              ))}
            </SelectField>
            <SelectField
              label="Exchange"
              name="exchangeId"
              value={exchangeId}
              onChange={(e) => setExchangeId(e.target.value)}
              required
              className="w-32"
            >
              <option value="" disabled>
                —
              </option>
              {exchanges.map((ex) => (
                <option key={ex.id} value={ex.id}>
                  {ex.name}
                </option>
              ))}
            </SelectField>
            <TextField
              label="Cantidad"
              name="quantity"
              type="number"
              step="any"
              min="0"
              value={quantity}
              onChange={(e) => setQuantity(e.target.value)}
              required
              className="w-28"
            />
            <TextField
              label="Precio"
              name="pricePerUnit"
              type="number"
              step="any"
              min="0"
              value={pricePerUnit}
              onChange={(e) => setPricePerUnit(e.target.value)}
              required
              className="w-28"
            />
            <TextField
              label="Fecha"
              name="recordedAt"
              type="date"
              value={recordedAt}
              onChange={(e) => setRecordedAt(e.target.value)}
              required
              className="w-36"
            />
            <Button type="submit" disabled={createMutation.isPending}>
              {createMutation.isPending ? "Añadiendo…" : "Añadir entrada"}
            </Button>
          </form>
        ) : null}
        {createMutation.isError && (
          <p role="alert" className="border-t border-rule px-4 py-2 text-sm text-accent">
            {getApiErrorMessage(createMutation.error) ?? "No se pudo añadir la entrada."}
          </p>
        )}
        {referenceDataFailed ? (
          <ErrorNotice>
            No se pudieron cargar las criptomonedas o exchanges disponibles.
          </ErrorNotice>
        ) : (
          !canAddEntry && (
            <p className="border-t border-rule px-4 py-3 text-sm text-ink-muted">
              Necesitas al menos una{" "}
              <Link href="/cryptocurrencies" className="underline">
                criptomoneda
              </Link>{" "}
              y un{" "}
              <Link href="/exchanges" className="underline">
                exchange
              </Link>{" "}
              registrados antes de poder añadir una entrada.
            </p>
          )
        )}
      </section>
    </div>
  );
}

function EditEntryRow({
  entry,
  portfolioId,
  cryptoCurrencies,
  exchanges,
  onDone,
}: {
  entry: PortfolioEntryResponse;
  portfolioId: number;
  cryptoCurrencies: CryptoCurrencyResponse[];
  exchanges: ExchangeResponse[];
  onDone: () => void;
}) {
  const [cryptoCurrencyId, setCryptoCurrencyId] = useState(String(entry.cryptoCurrencyId));
  const [exchangeId, setExchangeId] = useState(String(entry.exchangeId));
  const [quantity, setQuantity] = useState(String(entry.quantity));
  const [pricePerUnit, setPricePerUnit] = useState(String(entry.pricePerUnit));
  const [recordedAt, setRecordedAt] = useState(entry.recordedAt.slice(0, 10));
  const updateMutation = useUpdatePortfolioEntry(entry.id, portfolioId);

  function handleSave() {
    if (!cryptoCurrencyId || !exchangeId || !quantity || !pricePerUnit) return;
    updateMutation.mutate(
      {
        cryptoCurrencyId: Number(cryptoCurrencyId),
        exchangeId: Number(exchangeId),
        quantity: Number(quantity),
        pricePerUnit: Number(pricePerUnit),
        recordedAt: new Date(recordedAt).toISOString(),
      },
      { onSuccess: onDone }
    );
  }

  const inputClassName =
    "w-full border border-rule bg-paper px-2 py-1 text-sm focus-visible:outline-2 focus-visible:outline-ink";

  return (
    <tr className="border-b border-rule bg-paper-raised last:border-0">
      <td className="px-4 py-2">
        <select
          value={cryptoCurrencyId}
          onChange={(e) => setCryptoCurrencyId(e.target.value)}
          className={inputClassName}
        >
          {cryptoCurrencies.map((c) => (
            <option key={c.id} value={c.id}>
              {c.symbol}
            </option>
          ))}
        </select>
      </td>
      <td className="px-4 py-2">
        <select
          value={exchangeId}
          onChange={(e) => setExchangeId(e.target.value)}
          className={inputClassName}
        >
          {exchanges.map((ex) => (
            <option key={ex.id} value={ex.id}>
              {ex.name}
            </option>
          ))}
        </select>
      </td>
      <td className="px-4 py-2">
        <input
          type="number"
          step="any"
          value={quantity}
          onChange={(e) => setQuantity(e.target.value)}
          className={`tabular text-right ${inputClassName}`}
        />
      </td>
      <td className="px-4 py-2">
        <input
          type="number"
          step="any"
          value={pricePerUnit}
          onChange={(e) => setPricePerUnit(e.target.value)}
          className={`tabular text-right ${inputClassName}`}
        />
      </td>
      <td className="tabular px-4 py-2 text-right text-ink-muted">
        {formatCurrency(Number(quantity || 0) * Number(pricePerUnit || 0))}
      </td>
      <td className="px-4 py-2">
        <input
          type="date"
          value={recordedAt}
          onChange={(e) => setRecordedAt(e.target.value)}
          className={inputClassName}
        />
      </td>
      <td className="px-4 py-2 text-right">
        <span className="inline-flex items-center gap-3">
          <IconButton aria-label="Guardar" onClick={handleSave} disabled={updateMutation.isPending}>
            <Check size={14} strokeWidth={1.75} aria-hidden />
          </IconButton>
          <IconButton aria-label="Cancelar" onClick={onDone}>
            <X size={14} strokeWidth={1.75} aria-hidden />
          </IconButton>
        </span>
      </td>
    </tr>
  );
}
