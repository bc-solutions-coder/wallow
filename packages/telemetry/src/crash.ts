import { NANOSECONDS_PER_MILLISECOND } from "./limits.js";
import { spawnSync } from "node:child_process";
import { sanitizeException } from "./privacy.js";

/** Node exits immediately after this monitor; a bounded child preserves normal crash semantics. */
export function monitorCrashes(options: {
  endpoint: string;
  credential: string;
  environment: string;
  release: string;
  delivered: WeakSet<object>;
}) {
  const monitor = (error: Error): void => {
    if (options.delivered.has(error)) {
      return;
    }

    try {
      const payload = {
        resourceLogs: [
          {
            scopeLogs: [
              {
                logRecords: [
                  {
                    timeUnixNano: (BigInt(Date.now()) * NANOSECONDS_PER_MILLISECOND).toString(),
                    severityNumber: 17,
                    severityText: "ERROR",
                    body: { stringValue: "exception.unexpected" },
                    attributes: Object.entries(sanitizeException(error)).map(([key, value]) => ({
                      key,
                      value: { stringValue: String(value) },
                    })),
                  },
                ],
              },
            ],
          },
        ],
      };
      // Secrets travel through stdin, never command arguments or inherited environment additions.
      spawnSync(
        process.execPath,
        [
          "--input-type=module",
          "-e",
          `
        let input = "";
        for await (const chunk of process.stdin) input += chunk;
        const { endpoint, credential, environment, release, payload } = JSON.parse(input);
        try {
          const response = await fetch(endpoint + "/v1/logs", {
            method: "POST", redirect: "error", signal: AbortSignal.timeout(4000),
            headers: { "content-type": "application/json", authorization: "Bearer " + credential,
              "x-wallow-environment": environment, "x-wallow-release": release },
            body: JSON.stringify(payload),
          });
          await response.body?.cancel();
        } catch {}
      `,
        ],
        {
          input: JSON.stringify({ ...options, delivered: undefined, payload }),
          timeout: 5000,
          stdio: ["pipe", "ignore", "ignore"],
        },
      );
    } catch {
      /* Preserve Node's existing uncaught-exception behavior. */
    }
  };
  process.on("uncaughtExceptionMonitor", monitor);
  return () => {
    process.removeListener("uncaughtExceptionMonitor", monitor);
  };
}
