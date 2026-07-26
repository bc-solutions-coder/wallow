/**
 * `createMfaClient(unwrap)` — the shared, unwrap-INJECTED MFA API-wrapper slice
 * (Wallow-0q2s.9.3).
 *
 * The generated MFA ops are mocked here because this module is (with the two app
 * facades) permitted to import them: these tests assert DELEGATION (right op,
 * right argument) and ENVELOPE ROUTING (the op's `{ data, error }` envelope is
 * handed to the INJECTED `unwrap`, whose result — success or throw — is returned
 * verbatim). The injection is the whole point of the 9.3 decision: the slice is
 * error-policy AGNOSTIC, so wallow-web's raw-throw `unwrap` and wallow-auth's
 * `WallowError`-throwing `unwrap` both plug in without changing the slice.
 *
 * Most cases are table driven off {@link METHOD_CASES}, which doubles as the
 * op-to-method mapping contract: one row per method, naming the exact generated
 * op it must call and the exact argument object it must pass.
 */

import { beforeEach, describe, expect, it, vi } from "vitest";

import { unwrap as rawThrowUnwrap } from "./facade";
import type { MfaClient, MfaUnwrap } from "./mfa-client";

// Hoisted so the vi.mock factory and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  getV1IdentityMfaStatus: vi.fn(),
  postV1IdentityMfaEnrollTotp: vi.fn(),
  postV1IdentityMfaEnrollConfirm: vi.fn(),
  postV1IdentityMfaDisable: vi.fn(),
  postV1IdentityMfaBackupCodesRegenerate: vi.fn(),
}));

vi.mock("./generated", () => ({ ...mocks }));

/** Every generated-op spy, keyed by op name. */
type OpName = keyof typeof mocks;

/**
 * One row per MFA method: the op it must delegate to, how to invoke it, and the
 * exact argument the op must receive (`undefined` = called with no argument).
 */
interface MethodCase {
  /** MFA method name, used as the test title. */
  readonly method: keyof MfaClient;
  /** Generated op the method must call. */
  readonly op: OpName;
  /** Drive the method on a client instance. */
  readonly invoke: (mfa: MfaClient) => Promise<unknown>;
  /** Argument object the op must be called with, or `undefined` for a no-arg call. */
  readonly expectedArg: unknown;
}

const METHOD_CASES: readonly MethodCase[] = [
  {
    method: "status",
    op: "getV1IdentityMfaStatus",
    invoke: (mfa: MfaClient) => mfa.status(),
    expectedArg: undefined,
  },
  {
    method: "enrollTotp",
    op: "postV1IdentityMfaEnrollTotp",
    invoke: (mfa: MfaClient) => mfa.enrollTotp(),
    expectedArg: undefined,
  },
  {
    method: "confirmEnroll",
    op: "postV1IdentityMfaEnrollConfirm",
    invoke: (mfa: MfaClient) => mfa.confirmEnroll("S3CR3T", "123456"),
    expectedArg: { body: { secret: "S3CR3T", code: "123456" } },
  },
  {
    method: "disable",
    op: "postV1IdentityMfaDisable",
    invoke: (mfa: MfaClient) => mfa.disable("pw"),
    expectedArg: { body: { password: "pw" } },
  },
  {
    method: "regenerateBackupCodes",
    op: "postV1IdentityMfaBackupCodesRegenerate",
    invoke: (mfa: MfaClient) => mfa.regenerateBackupCodes("pw"),
    expectedArg: { body: { password: "pw" } },
  },
];

/** A pass-through unwrap: resolve `data`, throw the raw `error` — a stand-in spy. */
const passThroughUnwrap: MfaUnwrap = async <TData>(
  pending: Promise<{ data?: TData; error?: unknown }>,
): Promise<TData> => {
  const { data, error } = await pending;
  if (error !== undefined) {
    throw error;
  }
  return data as TData;
};

/** Build a client with the SDK's real raw-throw `unwrap` (wallow-web's injection). */
async function freshMfaClient(unwrap: MfaUnwrap = rawThrowUnwrap): Promise<MfaClient> {
  const mod = await import("./mfa-client");
  return mod.createMfaClient(unwrap);
}

describe("createMfaClient", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("builds a client from an injected unwrap", async () => {
    const mfa: MfaClient = await freshMfaClient();

    expect(mfa).toBeDefined();
  });

  it("exposes exactly the five MFA-management methods", async () => {
    const mfa: MfaClient = await freshMfaClient();
    const expected: readonly (keyof MfaClient)[] = [
      "status",
      "enrollTotp",
      "confirmEnroll",
      "disable",
      "regenerateBackupCodes",
    ];

    for (const method of expected) {
      expect(mfa[method]).toBeTypeOf("function");
    }
  });
});

describe.each(METHOD_CASES)("$method", ({ op, invoke, expectedArg }: MethodCase) => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it(`delegates to ${op} with the expected argument`, async () => {
    mocks[op].mockResolvedValue({ data: { marker: op } });
    const mfa: MfaClient = await freshMfaClient();

    await invoke(mfa);

    expect(mocks[op]).toHaveBeenCalledTimes(1);
    if (expectedArg === undefined) {
      expect(mocks[op]).toHaveBeenCalledWith();
    } else {
      expect(mocks[op]).toHaveBeenCalledWith(expectedArg);
    }
  });

  it("routes the op envelope through the injected unwrap and returns its data", async () => {
    const data = { marker: op };
    mocks[op].mockResolvedValue({ data });
    // A hand-rolled spy rather than vi.fn: wrapping the generic MfaUnwrap in
    // vi.fn collapses its type parameter, so track the call count directly.
    let unwrapCalls = 0;
    const injected: MfaUnwrap = <TData>(pending: Promise<{ data?: TData; error?: unknown }>) => {
      unwrapCalls += 1;
      return passThroughUnwrap(pending);
    };
    const mfa: MfaClient = await freshMfaClient(injected);

    const result: unknown = await invoke(mfa);

    expect(unwrapCalls).toBe(1);
    expect(result).toBe(data);
  });

  it("propagates whatever the injected unwrap throws on an { error } envelope", async () => {
    mocks[op].mockResolvedValue({ error: { detail: "nope" } });
    const sentinel = new Error("injected-policy-error");
    const throwingUnwrap: MfaUnwrap = () => Promise.reject(sentinel);
    const mfa: MfaClient = await freshMfaClient(throwingUnwrap);

    await expect(invoke(mfa)).rejects.toBe(sentinel);
  });
});

/**
 * The injection seam is the crux of the 9.3 decision: the slice must not bake in
 * either app's error semantics. These pin that it is policy-agnostic — the same
 * `{ error }` envelope surfaces as whatever the INJECTED unwrap chooses.
 */
describe("error-policy agnosticism (the injection seam)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("throws the RAW ProblemDetails by identity when wallow-web's raw-throw unwrap is injected", async () => {
    const problem = { status: 400, detail: "bad code", error: "invalid_code" };
    mocks.postV1IdentityMfaEnrollConfirm.mockResolvedValue({ error: problem });
    const mfa: MfaClient = await freshMfaClient(rawThrowUnwrap);

    // rejects.toBe pins IDENTITY: the exact raw object is thrown, so wallow-web's
    // errors.ts problemDetail() still reads (error as ProblemDetails).detail /
    // (error as { error }).error off it unchanged.
    await expect(mfa.confirmEnroll("S3CR3T", "000000")).rejects.toBe(problem);
  });

  it("throws whatever a WallowError-style unwrap produces when wallow-auth injects it", async () => {
    class FakeWallowError extends Error {
      readonly code: string;
      constructor(code: string) {
        super(code);
        this.name = "FakeWallowError";
        this.code = code;
      }
    }
    mocks.postV1IdentityMfaEnrollConfirm.mockResolvedValue({
      error: { status: 401, extensions: { code: "no_auth_session" } },
    });
    const wallowUnwrap: MfaUnwrap = () => Promise.reject(new FakeWallowError("no_auth_session"));
    const mfa: MfaClient = await freshMfaClient(wallowUnwrap);

    const thrown = (await mfa.confirmEnroll("S3CR3T", "000000").catch((e: unknown) => e)) as {
      code: string;
    };

    // wallow-auth's MfaEnrollForm reads error.code off the thrown WallowError —
    // the slice must let that shape through untouched.
    expect(thrown).toBeInstanceOf(FakeWallowError);
    expect(thrown.code).toBe("no_auth_session");
  });
});

/**
 * The generated MFA ops resolve `200: unknown` bodies (no `ProducesResponseType`
 * in the spec), so both apps hand-wrote these response interfaces and narrowed at
 * the render boundary. The slice centralizes both the interfaces and the
 * narrowing: a typed result flows straight through the injected unwrap.
 */
describe("typed responses over the untyped wire bodies", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("returns the enroll payload untouched (secret + qrUri)", async () => {
    const payload = { secret: "S3CR3T", qrUri: "otpauth://totp/Wallow" };
    mocks.postV1IdentityMfaEnrollTotp.mockResolvedValue({ data: payload });
    const mfa: MfaClient = await freshMfaClient();

    const result = await mfa.enrollTotp();

    expect(result).toBe(payload);
  });

  it("returns the status payload untouched (enabled + method + backupCodeCount)", async () => {
    const payload = { enabled: true, method: "totp", backupCodeCount: 8 };
    mocks.getV1IdentityMfaStatus.mockResolvedValue({ data: payload });
    const mfa: MfaClient = await freshMfaClient();

    const result = await mfa.status();

    expect(result).toBe(payload);
  });

  it("returns the confirm payload including one-time backup codes", async () => {
    const payload = { succeeded: true, backupCodes: ["aaaa-bbbb", "cccc-dddd"] };
    mocks.postV1IdentityMfaEnrollConfirm.mockResolvedValue({ data: payload });
    const mfa: MfaClient = await freshMfaClient();

    const result = await mfa.confirmEnroll("S3CR3T", "123456");

    expect(result).toBe(payload);
  });
});
