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
          "border border-rule bg-paper px-2.5 py-1.5 text-sm text-ink placeholder:text-ink-muted focus-visible:outline-2 focus-visible:outline-ink " +
          (className ?? "")
        }
        {...rest}
      />
    </label>
  );
}
