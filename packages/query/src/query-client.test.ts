import { QueryClient } from "@tanstack/react-query";
import { describe, expect, it, vi } from "vitest";

import {
  createQueryClient,
  handledFailure,
  toastedFailure,
  type UnhandledFailure,
} from "./query-client";

/**
 * `createQueryClient()` is the single source of the React Query client wired
 * into the router context and the `__root` `QueryClientProvider`. It applies an
 * explicit query policy (retry disabled — deterministic tests, no silent
 * backoff), mints a fresh client per call so an SSR request never shares cache
 * with another, and carries the ONE unhandled-failure hook an app uses to
 * toast: every mutation not marked handled, every query that opted in.
 */
describe("createQueryClient", () => {
  it("returns a QueryClient instance", () => {
    expect(createQueryClient()).toBeInstanceOf(QueryClient);
  });

  it("disables query retries by default", () => {
    const client = createQueryClient();
    expect(client.getDefaultOptions().queries?.retry).toBe(false);
  });

  it("mints a fresh client on every call (SSR request isolation)", () => {
    expect(createQueryClient()).not.toBe(createQueryClient());
  });
});

/** Runs a mutation that rejects with `error` under `meta`, swallowing the rejection. */
async function failMutation(client: QueryClient, error: unknown, meta?: Record<string, unknown>) {
  const observer = client.getMutationCache().build(client, {
    mutationFn: () => Promise.reject(error),
    ...(meta === undefined ? {} : { meta }),
  });
  await observer.execute(undefined).catch(() => undefined);
}

/** Fetches a query that rejects with `error` under `meta`, swallowing the rejection. */
async function failQuery(
  client: QueryClient,
  error: unknown,
  meta?: Record<string, unknown>,
  queryKey: unknown[] = ["failing", Math.random()],
) {
  await client
    .fetchQuery({
      queryKey,
      queryFn: () => Promise.reject(error),
      retry: false,
      ...(meta === undefined ? {} : { meta }),
    })
    .catch(() => undefined);
}

describe("createQueryClient({ onUnhandledFailure })", () => {
  it("reports a mutation that carries no handled flag", async () => {
    const onUnhandledFailure = vi.fn<(failure: UnhandledFailure) => void>();
    const client = createQueryClient({ onUnhandledFailure });
    const error = new Error("boom");

    await failMutation(client, error);

    expect(onUnhandledFailure).toHaveBeenCalledTimes(1);
    expect(onUnhandledFailure).toHaveBeenCalledWith({ kind: "mutation", error });
  });

  it("passes nothing but kind and error to the callback", async () => {
    const onUnhandledFailure = vi.fn<(failure: UnhandledFailure) => void>();
    const client = createQueryClient({ onUnhandledFailure });

    await failMutation(client, new Error("boom"));

    expect(Object.keys(onUnhandledFailure.mock.calls[0]?.[0] ?? {}).toSorted()).toEqual([
      "error",
      "kind",
    ]);
  });

  it("stays silent for a mutation marked handled", async () => {
    const onUnhandledFailure = vi.fn<(failure: UnhandledFailure) => void>();
    const client = createQueryClient({ onUnhandledFailure });

    await failMutation(client, new Error("boom"), handledFailure());

    expect(onUnhandledFailure).not.toHaveBeenCalled();
  });

  it("stays silent for a query that did not opt in", async () => {
    const onUnhandledFailure = vi.fn<(failure: UnhandledFailure) => void>();
    const client = createQueryClient({ onUnhandledFailure });

    await failQuery(client, new Error("boom"));
    await failQuery(client, new Error("boom"), { other: true });

    expect(onUnhandledFailure).not.toHaveBeenCalled();
  });

  it("reports a query marked toasted", async () => {
    const onUnhandledFailure = vi.fn<(failure: UnhandledFailure) => void>();
    const client = createQueryClient({ onUnhandledFailure });
    const error = new Error("boom");

    await failQuery(client, error, toastedFailure());

    expect(onUnhandledFailure).toHaveBeenCalledTimes(1);
    expect(onUnhandledFailure).toHaveBeenCalledWith({ kind: "query", error });
  });

  it("reports a toasted query once per failure streak", async () => {
    const onUnhandledFailure = vi.fn<(failure: UnhandledFailure) => void>();
    const client = createQueryClient({ onUnhandledFailure });
    const queryKey = ["flaky"];
    let broken = true;
    const refetch = () => client.refetchQueries({ queryKey });

    await client
      .fetchQuery({
        queryKey,
        queryFn: () => (broken ? Promise.reject(new Error("down")) : Promise.resolve("ok")),
        retry: false,
        meta: toastedFailure(),
      })
      .catch(() => undefined);
    await refetch();
    await refetch();
    expect(onUnhandledFailure).toHaveBeenCalledTimes(1);

    broken = false;
    await refetch();
    broken = true;
    await refetch();
    await refetch();

    expect(onUnhandledFailure).toHaveBeenCalledTimes(2);
  });

  it("does nothing without a callback", async () => {
    const client = createQueryClient();

    await expect(failMutation(client, new Error("boom"))).resolves.toBeUndefined();
    await expect(failQuery(client, new Error("boom"), toastedFailure())).resolves.toBeUndefined();
  });
});

describe("meta helpers", () => {
  it("handledFailure sets the flag and keeps existing meta", () => {
    expect(handledFailure()).toEqual({ failureHandled: true });
    expect(handledFailure({ audit: "x" })).toEqual({ audit: "x", failureHandled: true });
  });

  it("toastedFailure sets the flag and keeps existing meta", () => {
    expect(toastedFailure()).toEqual({ toastFailure: true });
    expect(toastedFailure({ audit: "x" })).toEqual({ audit: "x", toastFailure: true });
  });

  it("does not mutate the meta it composes with", () => {
    const meta = { audit: "x" };
    handledFailure(meta);
    toastedFailure(meta);
    expect(meta).toEqual({ audit: "x" });
  });
});
