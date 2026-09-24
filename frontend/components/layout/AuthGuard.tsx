"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAppStore } from "@/store/useAppStore";

// Wraps every route under app/(app) — redirects to /login once we know (post-hydration)
// that there is no session. Axios's own 401 interceptor is the fallback for a token
// that goes stale mid-session; this guard is what stops the initial wasted requests.
export function AuthGuard({ children }: { children: React.ReactNode }) {
  const hasHydrated = useAppStore((state) => state.hasHydrated);
  const token = useAppStore((state) => state.token);
  const router = useRouter();

  useEffect(() => {
    if (hasHydrated && !token) {
      router.replace("/login");
    }
  }, [hasHydrated, token, router]);

  if (!hasHydrated || !token) {
    return null;
  }

  return <>{children}</>;
}
