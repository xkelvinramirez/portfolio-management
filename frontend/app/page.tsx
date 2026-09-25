"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { LoadingScreen } from "@/components/layout/LoadingScreen";
import { useAppStore } from "@/store/useAppStore";

export default function RootPage() {
  const hasHydrated = useAppStore((state) => state.hasHydrated);
  const token = useAppStore((state) => state.token);
  const router = useRouter();

  useEffect(() => {
    if (!hasHydrated) return;
    router.replace(token ? "/dashboard" : "/login");
  }, [hasHydrated, token, router]);

  return <LoadingScreen />;
}
