// Distinct muted hues (not the ledger's sepia monochrome) so each asset reads apart
// at a glance across the history lines and the allocation slices. Deliberately clear
// of true red, which stays reserved exclusively for negative movement.
export const CHART_COLORS = [
  "#2a5a8c", // indigo blue
  "#2f7a5f", // teal green
  "#a9781f", // ochre gold
  "#6b3f8c", // plum violet
  "#3f6b6b", // slate teal
  "#7a4a2a", // umber brown
];

// Hashes a stable key (e.g. a crypto symbol) to a palette color so the same asset
// keeps the same color across independently-fetched charts (allocation, history),
// regardless of each chart's own sort order.
export function colorForKey(key: string, palette: string[] = CHART_COLORS): string {
  let hash = 0;
  for (let i = 0; i < key.length; i++) {
    hash = (hash * 31 + key.charCodeAt(i)) >>> 0;
  }
  return palette[hash % palette.length];
}
