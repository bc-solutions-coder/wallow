// Not the entry file — a module the browser entry owns. The rule reaches every
// non-server file in the directory, not just index.ts, because anything here can
// be pulled into the browser graph by one import.

// expect-error: wallow/logger-no-node-builtins
import { hostname } from "node:os";

export const HOST: string = hostname();
