interface ErrorNoticeProps {
  children: React.ReactNode;
}

// Distinguishes "the request failed" from "there is nothing here yet" — showing an
// empty state for a failed fetch is a misleading state, not a cosmetic gap.
export function ErrorNotice({ children }: ErrorNoticeProps) {
  return (
    <p role="alert" className="px-4 py-8 text-center text-sm text-accent">
      {children}
    </p>
  );
}
