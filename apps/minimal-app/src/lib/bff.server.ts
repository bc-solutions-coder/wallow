import {
  createWallowBffServer,
  type NodeRedisClient,
  type WallowBffServer,
} from "@bc-solutions-coder/sdk/server";
import { createClient, type RedisClientType } from "redis";

/**
 * PROTOTYPE — the host's half of the BFF: connect Valkey when `REDIS_URL` is
 * set, hand the client to the preset, build lazily on first use.
 *
 * This is a near-verbatim copy of wallow-web's `bff.server.ts`, which is the
 * finding: ~40 lines of node-redis bridging every consumer will paste. See the
 * README "Gaps" section.
 */
let pending: Promise<WallowBffServer> | undefined;

function adaptRedisClient(redisClient: RedisClientType): NodeRedisClient {
  return {
    get: (key) => redisClient.get(key),
    set: (key, value, options) =>
      options === undefined
        ? redisClient.set(key, value)
        : redisClient.set(key, value, {
            ...(options.EX === undefined ? {} : { EX: options.EX }),
            ...(options.NX === true ? { NX: true } : {}),
          }),
    del: (key) => redisClient.del(key),
  };
}

async function build(): Promise<WallowBffServer> {
  const redisUrl = (process.env.REDIS_URL ?? "").trim();
  if (redisUrl === "") {
    return createWallowBffServer();
  }
  const redisClient: RedisClientType = createClient({ url: redisUrl });
  await redisClient.connect();
  return createWallowBffServer({ redisClient: adaptRedisClient(redisClient) });
}

function getBffServer(): Promise<WallowBffServer> {
  pending ??= build().catch((error: unknown) => {
    pending = undefined;
    throw error;
  });
  return pending;
}

export async function handleBffRequest(request: Request): Promise<Response> {
  return (await getBffServer()).handleBff(request);
}

export async function handleApiRequest(request: Request): Promise<Response> {
  return (await getBffServer()).handleApi(request);
}
