import { BrandMark } from "@/components/ui/BrandMark";

// Shown while AuthGuard/GuestGuard wait on store hydration before they know
// whether to redirect — replaces a blank flash with a quiet, on-brand hold.
export function LoadingScreen() {
  return (
    <div
      role="status"
      aria-label="Cargando"
      className="flex min-h-full flex-1 items-center justify-center"
    >
      <BrandMark size={28} className="animate-pulse" />
    </div>
  );
}
