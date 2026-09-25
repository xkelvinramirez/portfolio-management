import type { InputHTMLAttributes } from "react";

interface TextFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string;
}

export function TextField({ label, id, name, className, ...rest }: TextFieldProps) {
  const inputId = id ?? name;
  return (
    <label htmlFor={inputId} className="flex flex-col gap-1 text-xs text-ink-muted">
      {label}
      <input
        id={inputId}
        name={name}
        className={
          "rounded-lg border border-rule bg-paper px-3 py-2 text-sm text-ink placeholder:text-ink-muted focus-visible:outline-2 focus-visible:outline-brand " +
          (className ?? "")
        }
        {...rest}
      />
    </label>
  );
}
