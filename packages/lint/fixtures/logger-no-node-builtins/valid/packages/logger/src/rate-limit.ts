// A server-owned helper (part of the ./server graph, per packages/logger/CLAUDE.md).
// The exemption is the three-file allowlist, not just server.ts itself.
import { setTimeout as delay } from "node:timers/promises";

export async function backoff(): Promise<void> {
  await delay(1);
}
