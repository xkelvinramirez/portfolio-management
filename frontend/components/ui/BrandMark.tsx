interface BrandMarkProps {
  size?: number;
  className?: string;
}

// The same ruled-ledger-with-one-flagged-cell glyph as the browser tab icon
// (app/icon.svg), inlined so it can read the page's own CSS custom properties
// and take part in page-level animation (e.g. a loading pulse) instead of being
// a static asset request.
export function BrandMark({ size = 20, className }: BrandMarkProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 32 32" aria-hidden className={className}>
      <rect width="32" height="32" rx="7" fill="var(--ink)" />
      <rect x="7" y="8.2" width="18" height="2.2" rx="0.4" fill="var(--paper)" />
      <rect x="7" y="14.9" width="18" height="2.2" rx="0.4" fill="var(--paper)" />
      <rect x="7" y="21.6" width="12.5" height="2.2" rx="0.4" fill="var(--paper)" />
      <rect x="22" y="20.6" width="3.6" height="4.2" rx="0.6" fill="var(--accent)" />
    </svg>
  );
}
