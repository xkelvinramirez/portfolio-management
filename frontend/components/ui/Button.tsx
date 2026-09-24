import type { ButtonHTMLAttributes } from "react";

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: "primary" | "ghost" | "danger";
}

const VARIANT_CLASSES: Record<NonNullable<ButtonProps["variant"]>, string> = {
  primary: "bg-ink text-paper hover:bg-ink/90",
  ghost: "text-ink-muted hover:text-ink",
  // "danger" reads as a correction to the ledger (deleting/undoing an entry), which is
  // exactly what the accent is reserved for — not a generic warning color.
  danger: "text-accent hover:text-accent/80",
};

// Exported so a <Link> that should look like a button (e.g. an empty state's call to
// action) can reuse the exact same visual style without duplicating it by hand.
export function buttonClassName(variant: NonNullable<ButtonProps["variant"]> = "primary") {
  return (
    "inline-block px-3 py-1.5 text-sm font-medium transition-colors disabled:pointer-events-none disabled:opacity-40 " +
    VARIANT_CLASSES[variant]
  );
}

export function Button({ variant = "primary", className, type = "button", ...rest }: ButtonProps) {
  return (
    <button
      type={type}
      className={buttonClassName(variant) + " " + (className ?? "")}
      {...rest}
    />
  );
}
