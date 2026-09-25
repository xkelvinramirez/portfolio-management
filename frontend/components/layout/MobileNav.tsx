"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { NAV_ITEMS } from "./navItems";

export function MobileNav() {
  const pathname = usePathname();

  return (
    <nav
      aria-label="Navegación principal"
      className="flex overflow-x-auto bg-paper px-2 shadow-[0_1px_0_rgba(16,24,40,0.06)] md:hidden"
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
                ? "border-brand text-brand font-semibold"
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
