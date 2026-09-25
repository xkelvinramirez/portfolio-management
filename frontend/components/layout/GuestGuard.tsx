"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAppStore } from "@/store/useAppStore";
import { LoadingScreen } from "./LoadingScreen";

// Wraps /login and /register — sends an already-authenticated visitor straight to the
// dashboard instead of showing them a login form again.
export function GuestGuard({ children }: { children: React.ReactNode }) {
  const hasHydrated = useAppStore((state) => state.hasHydrated);
  const token = useAppStore((state) => state.token);
  const router = useRouter();

  useEffect(() => {
    if (hasHydrated && token) {
      router.replace("/dashboard");
    }
  }, [hasHydrated, token, router]);

  if (!hasHydrated || token) {
    return <LoadingScreen />;
  }

  return <>{children}</>;
}
