export interface ApiError {
  code?: string;
  message?: string;
  field?: string | null;
}

/**
 * Normalizes any failure shape into a flat error list:
 * - uBookIt problem details carry an `errors` ARRAY of {code, message, field};
 * - ASP.NET model-binding problem details carry an `errors` DICTIONARY of
 *   field → messages;
 * - the backoffice http client's interceptors may THROW on non-2xx, so
 *   callers must pass caught values through here too.
 */
export const toApiErrors = (problemOrThrown: unknown, fallback: string): ApiError[] => {
  const problem = problemOrThrown as { errors?: unknown; title?: string } | undefined;
  const errors = problem?.errors;

  if (Array.isArray(errors) && errors.length > 0) {
    return errors as ApiError[];
  }

  if (errors !== null && typeof errors === "object") {
    const entries = Object.entries(errors as Record<string, string[]>);
    if (entries.length > 0) {
      return entries.flatMap(([field, messages]) => messages.map((message) => ({ field, message })));
    }
  }

  return [{ message: problem?.title ?? fallback }];
};
