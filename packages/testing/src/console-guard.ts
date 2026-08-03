/**
 * The console guard a browser project installs once, in its setup file.
 *
 * React reports real defects through `console.error` — a key collision, an
 * update outside `act`, a hook order change, a boundary catch — and none of them
 * fail anything. They scroll past in a run that reports green, so the defect
 * survives until it surfaces somewhere else as a symptom nobody connects back.
 *
 * Installed per project rather than per spec for the same reason
 * `./navigation-escape` is: the file that writes the noise is not the file a
 * reader would think to guard, so a guard a spec has to opt into never covers
 * the spec that needed it. Wrapping `console` (rather than replacing it) keeps
 * the message on the terminal, so the failure and the original output arrive
 * together.
 *
 * The record is CONSUMED, not cleared: a spec that deliberately drives an error
 * path reads its entries back with {@link consumeConsoleNoise} /
 * {@link expectConsoleError}, and anything it did not read is still in the
 * record when the project's `afterEach` runs. So a second, unexpected error
 * cannot hide behind an expected one.
 */

import { vi } from "vitest";

/** The two levels the guard records. React reports real defects through `error`. */
export type ConsoleNoiseLevel = "error" | "warn";

/** One recorded console call, with its arguments already formatted. */
export interface ConsoleNoise {
  readonly level: ConsoleNoiseLevel;
  readonly message: string;
}

/**
 * The opening words of every noise failure. A consumer matches on this rather
 * than on the whole sentence, which also carries each recorded message.
 */
export const CONSOLE_NOISE_MESSAGE = "Console noise leaked from the test";

/**
 * The opening words of the failure raised when a spec asserted noise that never
 * arrived. Distinct from {@link CONSOLE_NOISE_MESSAGE}, which is the opposite
 * defect.
 */
export const NO_CONSOLE_NOISE_MESSAGE = "No console noise was recorded";

/** The levels wrapped, in the order the guard installs them. */
const GUARDED_LEVELS: readonly ConsoleNoiseLevel[] = ["error", "warn"];

/** Every call recorded since the last clear, in arrival order. */
const noise: ConsoleNoise[] = [];

/**
 * The `console` whose methods are wrapped, so a second `install` on the same
 * page is a no-op while a genuinely new browser context still gets its own
 * wrappers. A setup file re-evaluates per context, and each context is a
 * different `console`.
 */
let guarded: Console | undefined;

/** The methods displaced by the wrappers, for {@link uninstallConsoleGuard}. */
const originals = new Map<ConsoleNoiseLevel, Console[ConsoleNoiseLevel]>();

/**
 * Render one console argument as text. An `Error` keeps its stack, which is
 * usually the only part naming the component that logged it; anything else that
 * survives `JSON.stringify` is shown structurally rather than as
 * `[object Object]`.
 */
function formatArgument(argument: unknown): string {
  if (typeof argument === "string") {
    return argument;
  }

  if (argument instanceof Error) {
    return argument.stack ?? String(argument);
  }

  try {
    return JSON.stringify(argument) ?? String(argument);
  } catch {
    return String(argument);
  }
}

function record(level: ConsoleNoiseLevel, args: readonly unknown[]): void {
  noise.push({ level, message: args.map(formatArgument).join(" ") });
}

/**
 * Wrap `console.error` and `console.warn` on the current page. Idempotent — a
 * project's setup file calls it once per browser context, and a second call must
 * neither double-record a message nor stack a second wrapper.
 */
export function installConsoleGuard(): void {
  if (guarded === globalThis.console) {
    return;
  }

  guarded = globalThis.console;
  originals.clear();

  for (const level of GUARDED_LEVELS) {
    const original = console[level].bind(console);
    originals.set(level, original);

    console[level] = (...args: unknown[]): void => {
      record(level, args);
      original(...args);
    };
  }
}

/**
 * Put the real methods back and release the idempotence latch.
 *
 * Only this module's own spec has business calling it: a project installs the
 * guard for the whole run, and a spec that merely wants its noise not to fail
 * the test consumes the record instead.
 */
export function uninstallConsoleGuard(): void {
  if (guarded === undefined) {
    return;
  }

  for (const [level, original] of originals) {
    console[level] = original;
  }

  originals.clear();
  guarded = undefined;
}

/** Every call recorded since the last clear, in arrival order. */
export function consoleNoise(): readonly ConsoleNoise[] {
  return [...noise];
}

/** Forget every recorded call. */
export function clearConsoleNoise(): void {
  noise.length = 0;
}

/** Options shared by the consuming helpers. */
export interface ConsumeConsoleNoiseOptions {
  /** How long to wait for the first entry to arrive, in ms. */
  readonly timeout?: number;
}

/**
 * Wait for at least one entry, then take everything the record holds at that
 * moment out of it and return it.
 *
 * Consuming rather than clearing is what keeps "a spec that forgot to assert
 * still fails" true: an entry nobody reads is still there for the project's
 * `afterEach`. Only what was READ is removed, by count rather than by emptying
 * the array, so a message written while this awaited survives to fail the test.
 */
export async function consumeConsoleNoise(
  options: ConsumeConsoleNoiseOptions = {},
): Promise<readonly ConsoleNoise[]> {
  await vi.waitFor(
    () => {
      if (noise.length === 0) {
        throw new Error(
          `${NO_CONSOLE_NOISE_MESSAGE}. Nothing this test did wrote to console.error or console.warn, so there was nothing to assert — check that the action under test actually runs, and that it fails the way the spec claims.`,
        );
      }
    },
    options.timeout === undefined ? undefined : { timeout: options.timeout },
  );

  const consumed: ConsoleNoise[] = [...noise];
  noise.splice(0, consumed.length);

  return consumed;
}

/**
 * Consume everything, and answer with the first error-level entry containing
 * `substring`.
 *
 * Everything is consumed, not just the match: React logs one boundary catch as
 * several entries, and leaving the others behind would fail the test in
 * `afterEach` over noise it deliberately provoked.
 */
export async function expectConsoleError(
  substring: string,
  options: ConsumeConsoleNoiseOptions = {},
): Promise<ConsoleNoise> {
  const consumed: readonly ConsoleNoise[] = await consumeConsoleNoise(options);
  const match: ConsoleNoise | undefined = consumed.find(
    (entry) => entry.level === "error" && entry.message.includes(substring),
  );

  if (match === undefined) {
    throw new Error(
      `No console.error containing ${JSON.stringify(substring)} among:\n${formatEntries(consumed)}`,
    );
  }

  return match;
}

/** One indented `[level] message` line per entry. */
function formatEntries(entries: readonly ConsoleNoise[]): string {
  return entries.map((entry) => `  [${entry.level}] ${entry.message}`).join("\n");
}

/**
 * Throw — naming each recorded message — when anything was written to
 * `console.error`/`console.warn` since the last clear, then clear, so one leak
 * fails one test rather than every test behind it. This is what a project's
 * `afterEach` calls.
 */
export function assertNoConsoleNoise(): void {
  if (noise.length === 0) {
    return;
  }

  const lines: string = formatEntries(noise);
  clearConsoleNoise();

  throw new Error(
    `${CONSOLE_NOISE_MESSAGE}. Fix the cause — React reports real defects this way. A spec that drives an error path on purpose reads the messages back with consumeConsoleNoise()/expectConsoleError() instead:\n${lines}`,
  );
}
