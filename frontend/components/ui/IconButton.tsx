import type { ButtonHTMLAttributes } from "react";

interface IconButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  "aria-label": string;
  tone?: "muted" | "danger";
}

export function IconButton({ tone = "muted", className, type = "button", ...rest }: IconButtonProps) {
  return (
    <button
      type={type}
      className={
        "text-ink-muted transition-colors disabled:pointer-events-none disabled:opacity-40 " +
        (tone === "danger" ? "hover:text-accent" : "hover:text-ink") +
        " " +
        (className ?? "")
      }
      {...rest}
    />
  );
}
