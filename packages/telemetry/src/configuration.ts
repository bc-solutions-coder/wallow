export interface TelemetryOptions {
  endpoint?: string;
  credential?: string;
  environment?: string;
  release?: string;
  /** Exact HTTP origins to which trace context may be sent. */
  ownedOrigins?: readonly string[];
}

const identifier = /^[A-Za-z0-9_.-]{1,128}$/u;
export function readTelemetryConfig(options: TelemetryOptions) {
  const endpoint = options.endpoint ?? process.env.WALLOW_TELEMETRY_ENDPOINT;
  const credential = options.credential ?? process.env.WALLOW_TELEMETRY_CREDENTIAL;
  const environment =
    options.environment ?? process.env.WALLOW_TELEMETRY_ENVIRONMENT ?? "production";
  const release = options.release ?? process.env.WALLOW_TELEMETRY_RELEASE ?? "unknown";
  if (endpoint === undefined || credential === undefined) {
    throw new Error("Telemetry endpoint and server credential are required");
  }
  const destination = new URL(endpoint);
  if (
    !/^https?:$/u.test(destination.protocol) ||
    destination.username !== "" ||
    destination.password !== "" ||
    destination.search !== "" ||
    destination.hash !== ""
  ) {
    throw new Error("Invalid telemetry endpoint");
  }
  if (!/^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$/u.test(credential)) {
    throw new Error("Invalid telemetry credential");
  }
  if (!identifier.test(environment) || !identifier.test(release)) {
    throw new Error("Invalid telemetry environment or release");
  }
  return { destination, credential, environment, release };
}
