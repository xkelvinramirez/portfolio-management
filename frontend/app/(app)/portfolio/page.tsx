"use client";

import { type FormEvent, useState } from "react";
import Link from "next/link";
import { useCreatePortfolio, useDeletePortfolio, usePortfolios } from "@/hooks/usePortfolios";
import { useAppStore } from "@/store/useAppStore";
import { Button } from "@/components/ui/Button";
import { ErrorNotice } from "@/components/ui/ErrorNotice";
import { RowActions } from "@/components/ui/RowActions";
import { TextField } from "@/components/ui/TextField";
import { formatDate } from "@/lib/format";
import { getApiErrorMessage } from "@/lib/errors";

export default function PortfolioListPage() {
  const userId = useAppStore((state) => state.userId);
  const { data, isPending, isError, error } = usePortfolios({
    limit: 100,
    userId: userId ?? undefined,
  });
  const createMutation = useCreatePortfolio();
  const deleteMutation = useDeletePortfolio();

  const items = data?.data ?? [];
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");

  function handleCreate(e: FormEvent) {
    e.preventDefault();
    if (!name.trim() || !userId) return;
    createMutation.mutate(
      { userId, name: name.trim(), description: description.trim() },
      { onSuccess: () => { setName(""); setDescription(""); } }
    );
  }

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-6">
      <div>
        <h1 className="text-xl font-semibold tracking-tight">Portfolios</h1>
        <p className="text-sm text-ink-muted">
          Cada portfolio lleva su propio registro de entradas por activo, exchange y fecha.
        </p>
      </div>

      <section className="border border-rule bg-paper">
        {isPending ? (
          <div className="h-32 animate-pulse bg-paper-raised" />
        ) : isError ? (
          <ErrorNotice>{getApiErrorMessage(error) ?? "No se pudieron cargar los portfolios."}</ErrorNotice>
        ) : (
          <table className="w-full border-collapse text-sm">
            <thead>
              <tr className="border-b border-rule-strong text-left text-xs tracking-wide text-ink-muted uppercase">
                <th className="px-4 py-2 font-medium">Nombre</th>
                <th className="px-4 py-2 font-medium">Descripción</th>
                <th className="px-4 py-2 font-medium">Creado</th>
                <th className="px-4 py-2 text-right font-medium">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {items.length === 0 && (
                <tr>
                  <td colSpan={4} className="px-4 py-8 text-center text-ink-muted">
                    Todavía no tienes portfolios.
                  </td>
                </tr>
              )}
              {items.map((item) => (
                <tr
                  key={item.id}
                  className="border-b border-rule last:border-0 hover:bg-paper-raised"
                >
                  <td className="px-4 py-2.5">
                    <Link
                      href={`/portfolio/${item.id}`}
                      className="font-medium underline-offset-2 hover:underline"
                    >
                      {item.name}
                    </Link>
                  </td>
                  <td className="px-4 py-2.5 text-ink-muted">{item.description || "—"}</td>
                  <td className="px-4 py-2.5 text-ink-muted">{formatDate(item.createdAt)}</td>
                  <td className="px-4 py-2.5 text-right">
                    <RowActions
                      onDelete={() => deleteMutation.mutate(item.id)}
                      deleting={deleteMutation.isPending && deleteMutation.variables === item.id}
                    />
                  </td>
                </tr>
              ))}
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
            placeholder="Mi portfolio principal"
            required
            className="w-56"
          />
          <TextField
            label="Descripción"
            name="description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="Opcional"
            className="w-64"
          />
          <Button type="submit" disabled={createMutation.isPending || !userId}>
            {createMutation.isPending ? "Creando…" : "Crear portfolio"}
          </Button>
        </form>
        {createMutation.isError && (
          <p role="alert" className="border-t border-rule px-4 py-2 text-sm text-accent">
            {getApiErrorMessage(createMutation.error) ?? "No se pudo crear el portfolio."}
          </p>
        )}
        {!userId && (
          <p className="border-t border-rule px-4 py-2 text-xs text-ink-muted">
            Inicia sesión para poder crear un portfolio.
          </p>
        )}
      </section>
    </div>
  );
}
