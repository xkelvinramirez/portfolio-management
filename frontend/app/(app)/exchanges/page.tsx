"use client";

import { type FormEvent, useState } from "react";
import { Check, Eye, EyeOff, X } from "lucide-react";
import {
  useCreateExchange,
  useDeleteExchange,
  useExchanges,
  useUpdateExchange,
} from "@/hooks/useExchanges";
import { Button } from "@/components/ui/Button";
import { ErrorNotice } from "@/components/ui/ErrorNotice";
import { IconButton } from "@/components/ui/IconButton";
import { RowActions } from "@/components/ui/RowActions";
import { TextField } from "@/components/ui/TextField";
import { formatDate } from "@/lib/format";
import { getApiErrorMessage } from "@/lib/errors";
import type { ExchangeResponse } from "@/types";

function maskApiKey(apiKey: string) {
  if (!apiKey) return "—";
  if (apiKey.length <= 4) return "••••";
  return `••••${apiKey.slice(-4)}`;
}

export default function ExchangesPage() {
  const { data, isPending, isError, error } = useExchanges({ limit: 100 });
  const createMutation = useCreateExchange();
  const deleteMutation = useDeleteExchange();

  const items = data?.data ?? [];
  const [editingId, setEditingId] = useState<number | null>(null);
  const [revealedId, setRevealedId] = useState<number | null>(null);
  const [name, setName] = useState("");
  const [apiKey, setApiKey] = useState("");

  function handleCreate(e: FormEvent) {
    e.preventDefault();
    if (!name.trim() || !apiKey.trim()) return;
    createMutation.mutate(
      { name: name.trim(), apiKey: apiKey.trim() },
      { onSuccess: () => { setName(""); setApiKey(""); } }
    );
  }

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Exchanges</h1>
        <p className="text-sm text-ink-muted">
          Los exchanges donde mantienes holdings. La API key es obligatoria por ahora, pero
          queda reservada — hoy no hay sincronización automática de precios.
        </p>
      </div>

      <section className="panel overflow-hidden">
        {isPending ? (
          <div className="h-32 animate-pulse bg-paper-raised" />
        ) : isError ? (
          <ErrorNotice>{getApiErrorMessage(error) ?? "No se pudieron cargar los exchanges."}</ErrorNotice>
        ) : (
          <table className="w-full border-collapse text-sm">
            <thead>
              <tr className="border-b border-rule-strong text-left text-xs tracking-wide text-ink-muted uppercase">
                <th className="px-4 py-2 font-medium">Nombre</th>
                <th className="px-4 py-2 font-medium">API key</th>
                <th className="px-4 py-2 font-medium">Creado</th>
                <th className="px-4 py-2 text-right font-medium">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {items.length === 0 && (
                <tr>
                  <td colSpan={4} className="px-4 py-8 text-center text-ink-muted">
                    Todavía no hay exchanges registrados.
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
                    <td className="px-4 py-2.5 font-medium">{item.name}</td>
                    <td className="tabular px-4 py-2.5 text-ink-muted">
                      <span className="inline-flex items-center gap-2">
                        {revealedId === item.id ? item.apiKey || "—" : maskApiKey(item.apiKey)}
                        <IconButton
                          aria-label={revealedId === item.id ? "Ocultar API key" : "Mostrar API key"}
                          onClick={() => setRevealedId(revealedId === item.id ? null : item.id)}
                        >
                          {revealedId === item.id ? (
                            <EyeOff size={14} strokeWidth={1.75} aria-hidden />
                          ) : (
                            <Eye size={14} strokeWidth={1.75} aria-hidden />
                          )}
                        </IconButton>
                      </span>
                    </td>
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
            label="Nombre"
            name="name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Binance"
            required
            className="w-40"
          />
          <TextField
            label="API key"
            name="apiKey"
            value={apiKey}
            onChange={(e) => setApiKey(e.target.value)}
            placeholder="Reservado, sin uso todavía"
            required
            className="w-56"
          />
          <Button type="submit" disabled={createMutation.isPending}>
            {createMutation.isPending ? "Añadiendo…" : "Añadir"}
          </Button>
        </form>
        {createMutation.isError && (
          <p role="alert" className="border-t border-rule bg-accent-soft px-4 py-2 text-sm text-accent">
            {getApiErrorMessage(createMutation.error) ?? "No se pudo añadir el exchange."}
          </p>
        )}
      </section>
    </div>
  );
}

function EditRow({ item, onDone }: { item: ExchangeResponse; onDone: () => void }) {
  const [name, setName] = useState(item.name);
  const [apiKey, setApiKey] = useState(item.apiKey);
  const updateMutation = useUpdateExchange(item.id);

  function handleSave() {
    if (!name.trim() || !apiKey.trim()) return;
    updateMutation.mutate({ name: name.trim(), apiKey: apiKey.trim() }, { onSuccess: onDone });
  }

  return (
    <tr className="border-b border-rule bg-paper-raised last:border-0">
      <td className="px-4 py-2">
        <input
          value={name}
          onChange={(e) => setName(e.target.value)}
          className="w-full rounded-lg border border-rule bg-paper px-2 py-1 text-sm focus-visible:outline-2 focus-visible:outline-brand"
        />
      </td>
      <td className="px-4 py-2">
        <input
          value={apiKey}
          onChange={(e) => setApiKey(e.target.value)}
          className="w-full rounded-lg border border-rule bg-paper px-2 py-1 text-sm focus-visible:outline-2 focus-visible:outline-brand"
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
