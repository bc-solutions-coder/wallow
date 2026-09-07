export interface TelemetryLogger {
  debug: (event: string, attrs?: Record<string, unknown>, error?: unknown) => void;
  info: (event: string, attrs?: Record<string, unknown>, error?: unknown) => void;
  warn: (event: string, attrs?: Record<string, unknown>, error?: unknown) => void;
  error: (event: string, attrs?: Record<string, unknown>, error?: unknown) => void;
  child: (attrs: Record<string, unknown>) => TelemetryLogger;
}
