import { LIMITS } from "./limits.js";

const EMPTY_QUEUE = 0;
export interface ExportStats {
  queuedBytes: number;
  exported: number;
  dropped: number;
  failed: number;
}

type Signal = "logs" | "traces" | "metrics";

/** One shared byte budget includes the in-flight request. Failed records are never requeued. */
export function createExporter(options: {
  endpoint: string;
  credential: string;
  environment: string;
  release: string;
}) {
  const queue: { signal: Signal; body: string; bytes: number; onExported?: () => void }[] = [];
  const stats: ExportStats = { queuedBytes: 0, exported: 0, dropped: 0, failed: 0 };
  let stopped = false;
  let exporting: Promise<void> | undefined;
  const encoder = new TextEncoder();
  const abort = new AbortController();
  async function drain(): Promise<void> {
    while (queue.length > EMPTY_QUEUE && !abort.signal.aborted) {
      const item = queue.shift();
      if (item === undefined) {
        break;
      }
      try {
        // One in-flight request keeps exporter concurrency and memory bounded.
        // eslint-disable-next-line no-await-in-loop
        const response = await fetch(`${options.endpoint}/v1/${item.signal}`, {
          method: "POST",
          body: item.body,
          headers: {
            "content-type": "application/json",
            authorization: `Bearer ${options.credential}`,
            "x-wallow-environment": options.environment,
            "x-wallow-release": options.release,
          },
          signal: AbortSignal.any([abort.signal, AbortSignal.timeout(LIMITS.exportMs)]),
          redirect: "error",
        });
        // eslint-disable-next-line no-await-in-loop -- Release this response before sending the next.
        await response.body?.cancel();
        if (response.ok) {
          stats.exported += 1;
          item.onExported?.();
        } else {
          stats.failed += 1;
        }
      } catch {
        stats.failed += 1;
      } finally {
        stats.queuedBytes -= item.bytes;
      }
    }
  }
  function flush(): Promise<void> {
    exporting ??= drain().finally(() => {
      exporting = undefined;
    });
    return exporting;
  }
  const timer = setInterval(() => {
    void flush();
  }, LIMITS.exportMs);
  timer.unref();
  return {
    enqueue(signal: Signal, payload: unknown, onExported?: () => void): void {
      if (stopped) {
        stats.dropped += 1;
        return;
      }
      let body: string;
      try {
        body = JSON.stringify(payload);
      } catch {
        stats.dropped += 1;
        return;
      }
      const bytes = encoder.encode(body).byteLength;
      if (bytes > LIMITS.requestBytes || stats.queuedBytes + bytes > LIMITS.bufferedBytes) {
        stats.dropped += 1;
        return;
      }
      queue.push({ signal, body, bytes, onExported });
      stats.queuedBytes += bytes;
    },
    flush,
    stats: (): ExportStats => ({ ...stats }),
    async shutdown(): Promise<void> {
      stopped = true;
      clearInterval(timer);
      const deadline = setTimeout(() => abort.abort(), LIMITS.exportMs);
      try {
        await flush();
      } finally {
        clearTimeout(deadline);
        stats.dropped += queue.length;
        queue.length = 0;
        stats.queuedBytes = 0;
      }
    },
  };
}
