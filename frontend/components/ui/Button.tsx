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

export function Button({ variant = "primary", className, type = "button", ...rest }: ButtonProps) {
  return (
    <button
      type={type}
      className={
        "px-3 py-1.5 text-sm font-medium transition-colors disabled:pointer-events-none disabled:opacity-40 " +
        VARIANT_CLASSES[variant] +
        " " +
        (className ?? "")
      }
      {...rest}
    />
  );
}
