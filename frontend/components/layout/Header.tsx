"use client";

import { useRouter } from "next/navigation";
import { LogOut } from "lucide-react";
import { useAppStore } from "@/store/useAppStore";
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
    <header className="flex items-center justify-between border-b border-rule px-4 py-3 md:px-6">
      <span className="text-sm font-semibold tracking-tight">Portfolio Ledger</span>
      {userEmail && (
        <span className="flex items-center gap-3">
          <span className="text-xs text-ink-muted">{userEmail}</span>
          <IconButton aria-label="Cerrar sesión" onClick={handleLogout}>
            <LogOut size={16} strokeWidth={1.75} aria-hidden />
          </IconButton>
        </span>
      )}
    </header>
  );
}
