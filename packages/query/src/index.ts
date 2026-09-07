/**
 * Re-export TanStack React Query bindings by reference and add Wallow client and
 * failure-reporting helpers. Providers and hooks must resolve the same React Query instance.
 */
export * from "@tanstack/react-query";

export {
  createQueryClient,
  type CreateQueryClientOptions,
  handledFailure,
  toastedFailure,
  type UnhandledFailure,
} from "./query-client";
