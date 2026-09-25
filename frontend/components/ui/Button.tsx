import type { ButtonHTMLAttributes } from "react";

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: "primary" | "ghost" | "danger";
}

const VARIANT_CLASSES: Record<NonNullable<ButtonProps["variant"]>, string> = {
  primary: "bg-brand text-white hover:bg-brand/90",
  ghost: "text-ink-muted hover:text-ink",
  // "danger" is reserved for a destructive/correcting action — the same red
  // reserved everywhere else for negative movement, never a generic warning.
  danger: "text-accent hover:text-accent/80",
};

// Exported so a <Link> that should look like a button (e.g. an empty state's call to
// action) can reuse the exact same visual style without duplicating it by hand.
export function buttonClassName(variant: NonNullable<ButtonProps["variant"]> = "primary") {
  return (
    "inline-block rounded-lg px-3.5 py-2 text-sm font-semibold transition-colors disabled:pointer-events-none disabled:opacity-40 " +
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
