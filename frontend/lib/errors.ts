import { isAxiosError } from "axios";

// Every backend error response is `List<Error>` (ErrorOr), serialized as an array of
// { code, description, ... }. This surfaces the first one's human-readable text —
// e.g. "A portfolio with the name 'Principal' already exists." — instead of a generic
// "something went wrong" that hides what the user actually needs to fix.
export function getApiErrorMessage(error: unknown): string | null {
  if (isAxiosError(error)) {
    const data: unknown = error.response?.data;
    if (Array.isArray(data) && typeof data[0]?.description === "string") {
      return data[0].description;
    }
  }
  return null;
}
