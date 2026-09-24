"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { BookOpen, Coins, ArrowLeftRight, Settings, Wallet } from "lucide-react";

const NAV_ITEMS = [
  { href: "/dashboard", label: "Dashboard", icon: BookOpen },
  { href: "/portfolio", label: "Portfolio", icon: Wallet },
  { href: "/cryptocurrencies", label: "Cripto", icon: Coins },
  { href: "/exchanges", label: "Exchanges", icon: ArrowLeftRight },
  { href: "/settings", label: "Ajustes", icon: Settings },
];

export function MobileNav() {
  const pathname = usePathname();

  return (
    <nav
      aria-label="Navegación principal"
      className="flex overflow-x-auto border-b border-rule bg-paper-raised px-2 md:hidden"
    >
      {NAV_ITEMS.map(({ href, label, icon: Icon }) => {
        const active = pathname === href || pathname?.startsWith(`${href}/`);
        return (
          <Link
            key={href}
            href={href}
            aria-current={active ? "page" : undefined}
            className={
              "flex shrink-0 items-center gap-1.5 border-b-2 px-3 py-2.5 text-xs whitespace-nowrap transition-colors " +
              (active
                ? "border-ink text-ink font-medium"
                : "border-transparent text-ink-muted")
            }
          >
            <Icon size={14} strokeWidth={1.75} aria-hidden />
            {label}
          </Link>
        );
      })}
    </nav>
  );
}
