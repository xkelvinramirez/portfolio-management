interface ErrorNoticeProps {
  children: React.ReactNode;
}

// Distinguishes "the request failed" from "there is nothing here yet" — showing an
// empty state for a failed fetch is a misleading state, not a cosmetic gap.
export function ErrorNotice({ children }: ErrorNoticeProps) {
  return (
    <p role="alert" className="rounded-lg bg-accent-soft px-4 py-3 text-sm text-accent">
      {children}
    </p>
  );
}
