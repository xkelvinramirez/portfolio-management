interface SegmentedControlProps<T extends string> {
  options: { value: T; label: string }[];
  value: T;
  onChange: (value: T) => void;
  "aria-label": string;
}

// Active state is a lifted white pill on the gray track (shadow, not a fill
// color) — the accent stays reserved exclusively for negative values and
// corrections per the direction contract.
//
// role="group" + aria-pressed, not role="radiogroup"/"radio": a real radiogroup
// requires roving-tabindex arrow-key navigation between options, which these plain
// buttons don't implement — claiming the ARIA role without the behavior is worse
// than a plain toggle group.
export function SegmentedControl<T extends string>({
  options,
  value,
  onChange,
  ...rest
}: SegmentedControlProps<T>) {
  return (
    <div
      role="group"
      aria-label={rest["aria-label"]}
      className="inline-flex items-center gap-0.5 rounded-lg bg-paper-raised p-0.5"
    >
      {options.map((option) => {
        const active = option.value === value;
        return (
          <button
            key={option.value}
            type="button"
            aria-pressed={active}
            onClick={() => onChange(option.value)}
            className={
              "rounded-md px-2.5 py-1 text-xs font-semibold transition-colors " +
              (active ? "bg-paper text-ink shadow-sm" : "text-ink-muted hover:text-ink")
            }
          >
            {option.label}
          </button>
        );
      })}
    </div>
  );
}
