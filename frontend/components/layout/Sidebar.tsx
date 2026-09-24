"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { NAV_ITEMS } from "./navItems";

export function Sidebar() {
  const pathname = usePathname();

  return (
    <nav
      aria-label="Navegación principal"
      className="hidden w-56 shrink-0 border-r border-rule bg-paper-raised px-3 py-6 md:block"
    >
      <ul className="flex flex-col gap-0.5">
        {NAV_ITEMS.map(({ href, label, icon: Icon }) => {
          const active = pathname === href || pathname?.startsWith(`${href}/`);
          return (
            <li key={href}>
              <Link
                href={href}
                aria-current={active ? "page" : undefined}
                className={
                  "flex items-center gap-2.5 rounded-sm px-3 py-2 text-sm transition-colors " +
                  (active
                    ? "bg-ink text-paper font-medium"
                    : "text-ink-muted hover:bg-paper hover:text-ink")
                }
              >
                <Icon size={16} strokeWidth={1.75} aria-hidden />
                {label}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
