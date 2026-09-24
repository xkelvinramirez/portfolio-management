"use client";

import { type FormEvent, useState } from "react";
import { Check, X } from "lucide-react";
import {
  useCreateCryptoCurrency,
  useCryptoCurrencies,
  useDeleteCryptoCurrency,
  useUpdateCryptoCurrency,
} from "@/hooks/useCryptoCurrencies";
import { Button } from "@/components/ui/Button";
import { ErrorNotice } from "@/components/ui/ErrorNotice";
import { IconButton } from "@/components/ui/IconButton";
import { RowActions } from "@/components/ui/RowActions";
import { TextField } from "@/components/ui/TextField";
import { formatDate } from "@/lib/format";
import { getApiErrorMessage } from "@/lib/errors";
import type { CryptoCurrencyResponse } from "@/types";

export default function CryptoCurrenciesPage() {
  const { data, isPending, isError, error } = useCryptoCurrencies({ limit: 100 });
  const createMutation = useCreateCryptoCurrency();
  const deleteMutation = useDeleteCryptoCurrency();

  const items = data?.data ?? [];
  const [editingId, setEditingId] = useState<number | null>(null);
  const [symbol, setSymbol] = useState("");
  const [name, setName] = useState("");

  function handleCreate(e: FormEvent) {
    e.preventDefault();
    if (!symbol.trim() || !name.trim()) return;
    createMutation.mutate(
      { symbol: symbol.trim().toUpperCase(), name: name.trim() },
      { onSuccess: () => { setSymbol(""); setName(""); } }
    );
  }

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-6">
      <div>
        <h1 className="text-xl font-semibold tracking-tight">Criptomonedas</h1>
        <p className="text-sm text-ink-muted">
          Catálogo de activos disponibles para registrar en tus portfolios.
        </p>
      </div>

      <section className="border border-rule bg-paper">
        {isPending ? (
          <div className="h-32 animate-pulse bg-paper-raised" />
        ) : isError ? (
          <ErrorNotice>
            {getApiErrorMessage(error) ?? "No se pudieron cargar las criptomonedas."}
          </ErrorNotice>
        ) : (
          <table className="w-full border-collapse text-sm">
            <thead>
              <tr className="border-b border-rule-strong text-left text-xs tracking-wide text-ink-muted uppercase">
                <th className="px-4 py-2 font-medium">Símbolo</th>
                <th className="px-4 py-2 font-medium">Nombre</th>
                <th className="px-4 py-2 font-medium">Creado</th>
                <th className="px-4 py-2 text-right font-medium">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {items.length === 0 && (
                <tr>
                  <td colSpan={4} className="px-4 py-8 text-center text-ink-muted">
                    Todavía no hay criptomonedas registradas.
                  </td>
                </tr>
              )}
              {items.map((item) =>
                editingId === item.id ? (
                  <EditRow key={item.id} item={item} onDone={() => setEditingId(null)} />
                ) : (
                  <tr
                    key={item.id}
                    className="border-b border-rule last:border-0 hover:bg-paper-raised"
                  >
                    <td className="px-4 py-2.5 font-medium">{item.symbol}</td>
                    <td className="px-4 py-2.5 text-ink-muted">{item.name}</td>
                    <td className="px-4 py-2.5 text-ink-muted">{formatDate(item.createdAt)}</td>
                    <td className="px-4 py-2.5 text-right">
                      <RowActions
                        onEdit={() => setEditingId(item.id)}
                        onDelete={() => deleteMutation.mutate(item.id)}
                        deleting={
                          deleteMutation.isPending && deleteMutation.variables === item.id
                        }
                      />
                    </td>
                  </tr>
                )
              )}
            </tbody>
          </table>
        )}

        <form
          onSubmit={handleCreate}
          className="flex flex-wrap items-end gap-3 border-t border-rule px-4 py-3"
        >
          <TextField
            label="Símbolo"
            name="symbol"
            value={symbol}
            onChange={(e) => setSymbol(e.target.value)}
            placeholder="BTC"
            required
            maxLength={16}
            className="w-28"
          />
          <TextField
            label="Nombre"
            name="name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Bitcoin"
            required
            className="w-48"
          />
          <Button type="submit" disabled={createMutation.isPending}>
            {createMutation.isPending ? "Añadiendo…" : "Añadir"}
          </Button>
        </form>
        {createMutation.isError && (
          <p role="alert" className="border-t border-rule px-4 py-2 text-sm text-accent">
            {getApiErrorMessage(createMutation.error) ?? "No se pudo añadir la criptomoneda."}
          </p>
        )}
      </section>
    </div>
  );
}

function EditRow({
  item,
  onDone,
}: {
  item: CryptoCurrencyResponse;
  onDone: () => void;
}) {
  const [symbol, setSymbol] = useState(item.symbol);
  const [name, setName] = useState(item.name);
  const updateMutation = useUpdateCryptoCurrency(item.id);

  function handleSave() {
    if (!symbol.trim() || !name.trim()) return;
    updateMutation.mutate(
      { symbol: symbol.trim().toUpperCase(), name: name.trim() },
      { onSuccess: onDone }
    );
  }

  return (
    <tr className="border-b border-rule bg-paper-raised last:border-0">
      <td className="px-4 py-2">
        <input
          value={symbol}
          onChange={(e) => setSymbol(e.target.value)}
          className="w-full border border-rule bg-paper px-2 py-1 text-sm focus-visible:outline-2 focus-visible:outline-ink"
        />
      </td>
      <td className="px-4 py-2">
        <input
          value={name}
          onChange={(e) => setName(e.target.value)}
          className="w-full border border-rule bg-paper px-2 py-1 text-sm focus-visible:outline-2 focus-visible:outline-ink"
        />
      </td>
      <td className="px-4 py-2 text-ink-muted">{formatDate(item.createdAt)}</td>
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
