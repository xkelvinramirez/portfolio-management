import { QueryClient } from "@tanstack/react-query";

// A factory, not a shared singleton: Next.js App Router renders on the server per
// request, so a module-level QueryClient would leak cached data across users/requests.
// Providers (app/providers.tsx) creates one instance per browser session via useState.
export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 5 * 60 * 1000, // 5 min, matches the caching note in README.md
        retry: 1,
        refetchOnWindowFocus: false,
      },
    },
  });
}
