"use client";

import { useRouter } from "next/navigation";
import { LogOut } from "lucide-react";
import { useAppStore } from "@/store/useAppStore";
import { BrandMark } from "@/components/ui/BrandMark";
import { IconButton } from "@/components/ui/IconButton";

export function Header() {
  const userEmail = useAppStore((state) => state.userEmail);
  const clearSession = useAppStore((state) => state.clearSession);
  const router = useRouter();

  function handleLogout() {
    clearSession();
    router.push("/login");
  }

  return (
    <header className="flex items-center justify-between gap-3 bg-paper px-4 py-3 shadow-[0_1px_0_rgba(16,24,40,0.06)] md:px-6">
      <span className="flex shrink-0 items-center gap-2">
        <BrandMark size={22} />
        <span className="text-sm font-bold tracking-tight">Portfolio Ledger</span>
      </span>
      {userEmail && (
        <span className="flex min-w-0 items-center gap-3">
          <span className="flex size-7 shrink-0 items-center justify-center rounded-full bg-brand text-xs font-semibold text-white">
            {userEmail.charAt(0).toUpperCase()}
          </span>
          <span className="truncate text-xs text-ink-muted">{userEmail}</span>
          <IconButton aria-label="Cerrar sesión" onClick={handleLogout} className="shrink-0">
            <LogOut size={16} strokeWidth={1.75} aria-hidden />
          </IconButton>
        </span>
      )}
    </header>
  );
}
