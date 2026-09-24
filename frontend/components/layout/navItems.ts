import { ArrowLeftRight, BookOpen, Coins, Wallet } from "lucide-react";

// No "Ajustes" entry: that page doesn't exist yet, and a nav link to a 404 is a broken
// path, not a placeholder — add it back once /settings actually ships something.
export const NAV_ITEMS = [
  { href: "/dashboard", label: "Dashboard", icon: BookOpen },
  { href: "/portfolio", label: "Portfolio", icon: Wallet },
  { href: "/cryptocurrencies", label: "Criptomonedas", icon: Coins },
  { href: "/exchanges", label: "Exchanges", icon: ArrowLeftRight },
] as const;
