// Minimal JWT payload decoder — no verification, no external dependency. The backend
// already verifies the signature on every request; the frontend only needs to read
// claims (sub, email) from a token it already trusts because it just received it (or
// persisted it) from the API. Never use this to authorize anything client-side.

export interface AppJwtClaims {
  sub: string;
  email: string;
  jti: string;
  exp: number;
  iss: string;
  aud: string;
}

export function decodeJwtPayload<T = AppJwtClaims>(token: string): T | null {
  try {
    const payload = token.split(".")[1];
    if (!payload) return null;

    const normalized = payload.replace(/-/g, "+").replace(/_/g, "/");
    const padded = normalized.padEnd(normalized.length + ((4 - (normalized.length % 4)) % 4), "=");
    const json =
      typeof window !== "undefined"
        ? window.atob(padded)
        : Buffer.from(padded, "base64").toString("utf-8");

    return JSON.parse(json) as T;
  } catch {
    return null;
  }
}
