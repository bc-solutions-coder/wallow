import {
  BROWSER_LIMITS,
  record,
  sanitizeFaroBatch,
  faroEventCount,
  type FaroBatch,
} from "./faro-privacy.js";
import type { ExportStats } from "./exporter.js";

const START = 0;
const ONE = 1;
const CONTEXT_CHANGED = 409;
const encoder = new TextEncoder();
function merge(left: FaroBatch, right: FaroBatch): FaroBatch {
  const leftSpans = left.traces.resourceSpans;
  const rightSpans = right.traces.resourceSpans;
  return {
    logs: [...left.logs, ...right.logs],
    exceptions: [...left.exceptions, ...right.exceptions],
    measurements: [...left.measurements, ...right.measurements],
    events: [...left.events, ...right.events],
    traces: {
      resourceSpans: [
        ...(Array.isArray(leftSpans) ? leftSpans : []),
        ...(Array.isArray(rightSpans) ? rightSpans : []),
      ],
    },
  };
}

export function createBrowserQueue(relay: URL) {
  const send = globalThis.fetch.bind(globalThis);
  const queue: { wire: string; bytes: number }[] = [];
  const stats: ExportStats = { queuedBytes: START, exported: START, dropped: START, failed: START };
  let context: string | undefined;
  let generation = START;
  let abort = new AbortController();
  let flushing: Promise<void> | undefined;
  function clear(): void {
    stats.dropped += queue.length;
    for (const item of queue.splice(START)) {
      stats.queuedBytes -= item.bytes;
    }
  }
  async function resetContext(): Promise<void> {
    generation += ONE;
    const current = generation;
    context = undefined;
    clear();
    abort.abort();
    abort = new AbortController();
    try {
      const response = await send(relay, {
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        signal: AbortSignal.any([abort.signal, AbortSignal.timeout(BROWSER_LIMITS.flushMs)]),
      });
      const value = record(await response.json());
      if (
        response.ok &&
        typeof value.context === "string" &&
        value.context.length <= BROWSER_LIMITS.events &&
        generation === current
      ) {
        context = value.context;
      }
    } catch {
      stats.failed += ONE;
    }
  }
  function enqueue(input: unknown): void {
    if (context === undefined) {
      stats.dropped += ONE;
      return;
    }
    const sanitized = sanitizeFaroBatch(input, (count) => {
      stats.dropped += count;
    });
    if (faroEventCount(sanitized) === START) {
      return;
    }
    const wire = JSON.stringify(sanitized);
    const bytes = encoder.encode(wire).byteLength;
    if (
      bytes > BROWSER_LIMITS.eventBytes ||
      bytes + stats.queuedBytes > BROWSER_LIMITS.bufferedBytes
    ) {
      stats.dropped += ONE;
      return;
    }
    queue.push({ wire, bytes });
    stats.queuedBytes += bytes;
  }
  async function drain(): Promise<void> {
    const current = generation;
    const signal = AbortSignal.any([abort.signal, AbortSignal.timeout(BROWSER_LIMITS.flushMs)]);
    while (queue.length > START && context !== undefined && !signal.aborted) {
      if (current !== generation) {
        break;
      }
      let batch = sanitizeFaroBatch({});
      let wire = "";
      let heldBytes = START;
      let count = START;
      while (queue.length > START) {
        const next = queue[START];
        if (next === undefined) {
          break;
        }
        const candidate = merge(batch, sanitizeFaroBatch(JSON.parse(next.wire)));
        const candidateWire = JSON.stringify({ ...candidate, context });
        if (
          [
            candidate.logs,
            candidate.exceptions,
            candidate.events,
            candidate.measurements,
            candidate.traces.resourceSpans,
          ].some((items) => Array.isArray(items) && items.length > BROWSER_LIMITS.events) ||
          encoder.encode(candidateWire).byteLength > BROWSER_LIMITS.batchBytes
        ) {
          break;
        }
        queue.shift();
        heldBytes += next.bytes;
        count += ONE;
        batch = candidate;
        wire = candidateWire;
      }
      if (count === START) {
        clear();
        break;
      }
      try {
        // Serial batches share one flush deadline and include in-flight bytes in the budget.
        // eslint-disable-next-line no-await-in-loop
        const response = await send(relay, {
          method: "POST",
          credentials: "same-origin",
          headers: { "content-type": "application/json" },
          body: wire,
          keepalive: true,
          redirect: "error",
          signal,
        });
        if (current !== generation) {
          stats.dropped += count;
        } else if (response.status === CONTEXT_CHANGED) {
          context = undefined;
          clear();
          stats.dropped += count;
        } else if (response.ok) {
          stats.exported += count;
        } else {
          stats.failed += ONE;
          stats.dropped += count;
        }
        // Release this response before starting the next bounded batch.
        // eslint-disable-next-line no-await-in-loop
        await response.body?.cancel();
      } catch {
        stats.failed += ONE;
        stats.dropped += count;
      } finally {
        stats.queuedBytes -= heldBytes;
      }
    }
    if (current !== generation) {
      return;
    }
    if (signal.aborted) {
      clear();
    } else if (context === undefined) {
      await resetContext();
    }
  }
  function flush(): Promise<void> {
    flushing ??= drain().finally(() => {
      flushing = undefined;
    });
    return flushing;
  }
  return {
    enqueue,
    flush,
    resetContext,
    clear,
    epoch: () => generation,
    drop: () => {
      stats.dropped += ONE;
    },
    stats: () => ({ ...stats }),
  };
}
