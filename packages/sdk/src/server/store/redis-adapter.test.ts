import { describe, expect, it } from "vitest";

import { createRedisAdapter, type NodeRedisClient } from "./redis-adapter";
import { type RedisLike } from "./types";

/**
 * Records the calls the adapter forwards to the underlying node-redis client so
 * the option translation ({@link RedisLike} `{ex, nx}` to node-redis
 * `{EX, NX}`) and return-value narrowing can be asserted directly.
 */
class RecordingClient implements NodeRedisClient {
  public readonly calls: Array<{ method: string; args: unknown[] }> = [];
  public getReturn: string | null = null;
  public setReturn: string | null = "OK";
  public delReturn: number = 0;

  get(key: string): Promise<string | null> {
    this.calls.push({ method: "get", args: [key] });
    return Promise.resolve(this.getReturn);
  }

  set(key: string, value: string, options?: { EX?: number; NX?: boolean }): Promise<string | null> {
    this.calls.push({ method: "set", args: [key, value, options] });
    return Promise.resolve(this.setReturn);
  }

  del(key: string): Promise<number> {
    this.calls.push({ method: "del", args: [key] });
    return Promise.resolve(this.delReturn);
  }
}

describe("createRedisAdapter", () => {
  it("set maps {ex, nx} to node-redis {EX, NX} and returns OK on success", async () => {
    const client: RecordingClient = new RecordingClient();
    const adapter: RedisLike = createRedisAdapter(client);

    const result: "OK" | null = await adapter.set("k", "v", {
      ex: 60,
      nx: true,
    });

    expect(result).toBe("OK");
    expect(client.calls).toEqual([{ method: "set", args: ["k", "v", { EX: 60, NX: true }] }]);
  });

  it("set returns null when a conditional NX set is skipped", async () => {
    const client: RecordingClient = new RecordingClient();
    client.setReturn = null;
    const adapter: RedisLike = createRedisAdapter(client);

    const result: "OK" | null = await adapter.set("k", "v", { nx: true });

    expect(result).toBeNull();
    expect(client.calls[0].args[2]).toEqual({ NX: true });
  });

  it("set with no options forwards no options object", async () => {
    const client: RecordingClient = new RecordingClient();
    const adapter: RedisLike = createRedisAdapter(client);

    await adapter.set("k", "v");

    expect(client.calls[0].args[2]).toBeUndefined();
  });

  it("get delegates to the client and passes through both a value and null", async () => {
    const client: RecordingClient = new RecordingClient();
    const adapter: RedisLike = createRedisAdapter(client);

    expect(await adapter.get("missing")).toBeNull();
    client.getReturn = "value";
    expect(await adapter.get("present")).toBe("value");
    expect(client.calls).toEqual([
      { method: "get", args: ["missing"] },
      { method: "get", args: ["present"] },
    ]);
  });

  it("del delegates and returns the deleted count", async () => {
    const client: RecordingClient = new RecordingClient();
    client.delReturn = 1;
    const adapter: RedisLike = createRedisAdapter(client);

    const removed: number = await adapter.del("k");

    expect(removed).toBe(1);
    expect(client.calls).toEqual([{ method: "del", args: ["k"] }]);
  });
});
