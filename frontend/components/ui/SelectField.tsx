import type { SelectHTMLAttributes } from "react";

interface SelectFieldProps extends SelectHTMLAttributes<HTMLSelectElement> {
  label: string;
}

export function SelectField({ label, id, name, className, children, ...rest }: SelectFieldProps) {
  const inputId = id ?? name;
  return (
    <label htmlFor={inputId} className="flex flex-col gap-1 text-xs text-ink-muted">
      {label}
      <select
        id={inputId}
        name={name}
        className={
          "rounded-lg border border-rule bg-paper px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-brand " +
          (className ?? "")
        }
        {...rest}
      >
        {children}
      </select>
    </label>
  );
}
