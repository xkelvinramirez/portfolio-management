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
          "border border-rule bg-paper px-2.5 py-1.5 text-sm text-ink focus-visible:outline-2 focus-visible:outline-ink " +
          (className ?? "")
        }
        {...rest}
      >
        {children}
      </select>
    </label>
  );
}
