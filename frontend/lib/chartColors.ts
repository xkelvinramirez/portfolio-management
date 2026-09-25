// A vivid categorical palette so each asset reads apart at a glance across the
// history lines and the allocation slices. Deliberately clear of true red,
// which stays reserved exclusively for negative movement.
export const CHART_COLORS = [
  "#4f46e5", // indigo (brand)
  "#06b6d4", // cyan
  "#f59e0b", // amber
  "#f43f5e", // rose
  "#10b981", // emerald
  "#8b5cf6", // violet
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
