import { create } from "zustand";
import { persist } from "zustand/middleware";
import { decodeJwtPayload } from "@/lib/jwt";

interface AppState {
  token: string | null;
  tokenExpiresAt: string | null;
  userId: number | null;
  userEmail: string | null;
  setSession: (token: string, tokenExpiresAt: string) => void;
  clearSession: () => void;

  selectedPortfolioId: number | null;
  setSelectedPortfolioId: (portfolioId: number | null) => void;

  // Persisted state rehydrates from localStorage asynchronously after mount, so on the
  // very first render `token` is always null even for an already-logged-in user. Route
  // guards must wait for this flag before deciding to redirect.
  hasHydrated: boolean;
  setHasHydrated: (value: boolean) => void;
}

export const useAppStore = create<AppState>()(
  persist(
    (set) => ({
      token: null,
      tokenExpiresAt: null,
      userId: null,
      userEmail: null,
      // userId/userEmail are derived from the JWT's own claims (sub, email) — there is
      // no /users/me endpoint, so the token is the only source for either.
      setSession: (token, tokenExpiresAt) => {
        const claims = decodeJwtPayload(token);
        set({
          token,
          tokenExpiresAt,
          userId: claims ? Number(claims.sub) : null,
          userEmail: claims?.email ?? null,
        });
      },
      clearSession: () =>
        set({ token: null, tokenExpiresAt: null, userId: null, userEmail: null }),

      selectedPortfolioId: null,
      setSelectedPortfolioId: (portfolioId) => set({ selectedPortfolioId: portfolioId }),

      hasHydrated: false,
      setHasHydrated: (value) => set({ hasHydrated: value }),
    }),
    {
      name: "portfolio-app-store",
      partialize: (state) => ({
        token: state.token,
        tokenExpiresAt: state.tokenExpiresAt,
        userId: state.userId,
        userEmail: state.userEmail,
        selectedPortfolioId: state.selectedPortfolioId,
      }),
      onRehydrateStorage: () => (state) => {
        state?.setHasHydrated(true);
      },
    }
  )
);
