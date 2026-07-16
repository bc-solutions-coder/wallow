/**
 * `createAuthClient()` — the typed auth facade over the generated identity ops
 * (Wallow-vec7.2.1).
 *
 * The generated ops are mocked here because this facade is the ONLY module
 * permitted to import them: these tests assert DELEGATION (right op, right
 * arguments) and ENVELOPE UNWRAPPING (`data` out on success, a thrown
 * `WallowError` on `{ error }`), never the wire.
 *
 * Most cases are table driven off {@link METHOD_CASES}, which doubles as the
 * op-to-method mapping contract: one row per facade method, naming the exact
 * generated op it must call and the exact argument object it must pass.
 */

import { beforeEach, describe, expect, it, vi } from "vitest";

import type { AuthClient } from "./auth-client";
import { WallowError } from "./server/errors";

// Hoisted so the vi.mock factory and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  postV1IdentityAuthLogin: vi.fn(),
  postV1IdentityAuthRegister: vi.fn(),
  postV1IdentityAuthForgotPassword: vi.fn(),
  postV1IdentityAuthResetPassword: vi.fn(),
  getV1IdentityAuthVerifyEmail: vi.fn(),
  postV1IdentityAuthPasswordlessMagicLink: vi.fn(),
  getV1IdentityAuthPasswordlessMagicLinkVerify: vi.fn(),
  postV1IdentityAuthPasswordlessOtp: vi.fn(),
  postV1IdentityAuthPasswordlessOtpVerify: vi.fn(),
  postV1IdentityAuthMfaVerify: vi.fn(),
  postV1IdentityMfaEnrollTotp: vi.fn(),
  postV1IdentityMfaEnrollConfirm: vi.fn(),
  postV1IdentityMfaEnrollExchangeToken: vi.fn(),
  getV1IdentityAppsConsentInfoByClientId: vi.fn(),
  getV1IdentityAuthExternalProviders: vi.fn(),
  getV1IdentityAuthClientTenantByClientId: vi.fn(),
  getV1IdentityAppsByClientIdBranding: vi.fn(),
  getV1IdentityInvitationsVerifyByToken: vi.fn(),
  postV1IdentityInvitationsByTokenAccept: vi.fn(),
  getV1IdentityOrganizationDomainsMatch: vi.fn(),
  postV1IdentityMembershipRequests: vi.fn(),
  getV1IdentityAuthRedirectUriValidate: vi.fn(),
}));

vi.mock("./generated", () => ({ ...mocks }));

/** Every generated-op spy, keyed by op name. */
type OpName = keyof typeof mocks;

/**
 * One row per facade method: the op it must delegate to, how to invoke it, and
 * the exact argument the op must receive (`undefined` = called with no argument).
 */
interface MethodCase {
  /** Facade method name, used as the test title. */
  readonly method: string;
  /** Generated op the method must call. */
  readonly op: OpName;
  /** Drive the method on a facade instance. */
  readonly invoke: (auth: AuthClient) => Promise<unknown>;
  /** Argument object the op must be called with, or `undefined` for a no-arg call. */
  readonly expectedArg: unknown;
}

const LOGIN_BODY = { email: "user@example.com", password: "pw", rememberMe: true } as const;
const REGISTER_BODY = {
  email: "user@example.com",
  password: "pw",
  confirmPassword: "pw",
} as const;
const RESET_BODY = { email: "user@example.com", token: "t-1", newPassword: "pw2" } as const;
const MAGIC_LINK_BODY = { email: "user@example.com", returnUrl: "/dashboard" } as const;
const VERIFY_OTP_BODY = { email: "user@example.com", code: "123456" } as const;
const CONFIRM_ENROLL_BODY = { secret: "S3CR3T", code: "123456" } as const;

const METHOD_CASES: readonly MethodCase[] = [
  {
    method: "login",
    op: "postV1IdentityAuthLogin",
    invoke: (auth: AuthClient) => auth.login(LOGIN_BODY),
    expectedArg: { body: LOGIN_BODY },
  },
  {
    method: "register",
    op: "postV1IdentityAuthRegister",
    invoke: (auth: AuthClient) => auth.register(REGISTER_BODY),
    expectedArg: { body: REGISTER_BODY },
  },
  {
    method: "forgotPassword",
    op: "postV1IdentityAuthForgotPassword",
    invoke: (auth: AuthClient) => auth.forgotPassword({ email: "user@example.com" }),
    expectedArg: { body: { email: "user@example.com" } },
  },
  {
    method: "resetPassword",
    op: "postV1IdentityAuthResetPassword",
    invoke: (auth: AuthClient) => auth.resetPassword(RESET_BODY),
    expectedArg: { body: RESET_BODY },
  },
  {
    method: "verifyEmail",
    op: "getV1IdentityAuthVerifyEmail",
    invoke: (auth: AuthClient) => auth.verifyEmail({ email: "user@example.com", token: "t-1" }),
    expectedArg: { query: { email: "user@example.com", token: "t-1" } },
  },
  {
    method: "sendMagicLink",
    op: "postV1IdentityAuthPasswordlessMagicLink",
    invoke: (auth: AuthClient) => auth.sendMagicLink(MAGIC_LINK_BODY),
    expectedArg: { body: MAGIC_LINK_BODY },
  },
  {
    method: "verifyMagicLink",
    op: "getV1IdentityAuthPasswordlessMagicLinkVerify",
    invoke: (auth: AuthClient) => auth.verifyMagicLink({ token: "t-1", rememberMe: true }),
    expectedArg: { query: { token: "t-1", rememberMe: true } },
  },
  {
    method: "sendOtp",
    op: "postV1IdentityAuthPasswordlessOtp",
    invoke: (auth: AuthClient) => auth.sendOtp({ email: "user@example.com" }),
    expectedArg: { body: { email: "user@example.com" } },
  },
  {
    method: "verifyOtp",
    op: "postV1IdentityAuthPasswordlessOtpVerify",
    invoke: (auth: AuthClient) => auth.verifyOtp(VERIFY_OTP_BODY),
    expectedArg: { body: VERIFY_OTP_BODY },
  },
  {
    method: "verifyMfa",
    op: "postV1IdentityAuthMfaVerify",
    invoke: (auth: AuthClient) => auth.verifyMfa("123456"),
    expectedArg: { body: { code: "123456", useBackupCode: false } },
  },
  {
    method: "useBackupCode",
    op: "postV1IdentityAuthMfaVerify",
    invoke: (auth: AuthClient) => auth.useBackupCode("backup-1"),
    expectedArg: { body: { code: "backup-1", useBackupCode: true } },
  },
  {
    method: "enrollTotp",
    op: "postV1IdentityMfaEnrollTotp",
    invoke: (auth: AuthClient) => auth.enrollTotp(),
    expectedArg: undefined,
  },
  {
    method: "confirmEnrollment",
    op: "postV1IdentityMfaEnrollConfirm",
    invoke: (auth: AuthClient) => auth.confirmEnrollment(CONFIRM_ENROLL_BODY),
    expectedArg: { body: CONFIRM_ENROLL_BODY },
  },
  {
    method: "exchangeEnrollmentToken",
    op: "postV1IdentityMfaEnrollExchangeToken",
    invoke: (auth: AuthClient) => auth.exchangeEnrollmentToken("t-1"),
    expectedArg: { query: { token: "t-1" } },
  },
  {
    method: "getConsentInfo",
    op: "getV1IdentityAppsConsentInfoByClientId",
    invoke: (auth: AuthClient) => auth.getConsentInfo("client-1", ["openid", "profile"]),
    expectedArg: { path: { clientId: "client-1" }, query: { scopes: "openid,profile" } },
  },
  {
    method: "getExternalProviders",
    op: "getV1IdentityAuthExternalProviders",
    invoke: (auth: AuthClient) => auth.getExternalProviders(),
    expectedArg: undefined,
  },
  {
    method: "getClientTenant",
    op: "getV1IdentityAuthClientTenantByClientId",
    invoke: (auth: AuthClient) => auth.getClientTenant("client-1"),
    expectedArg: { path: { clientId: "client-1" } },
  },
  {
    method: "getClientBranding",
    op: "getV1IdentityAppsByClientIdBranding",
    invoke: (auth: AuthClient) => auth.getClientBranding("client-1"),
    expectedArg: { path: { clientId: "client-1" } },
  },
  {
    method: "verifyInvitation",
    op: "getV1IdentityInvitationsVerifyByToken",
    invoke: (auth: AuthClient) => auth.verifyInvitation("inv-1"),
    expectedArg: { path: { token: "inv-1" } },
  },
  {
    method: "acceptInvitation",
    op: "postV1IdentityInvitationsByTokenAccept",
    invoke: (auth: AuthClient) => auth.acceptInvitation("inv-1"),
    expectedArg: { path: { token: "inv-1" } },
  },
  {
    method: "getMatchingOrgByDomain",
    op: "getV1IdentityOrganizationDomainsMatch",
    invoke: (auth: AuthClient) => auth.getMatchingOrgByDomain("user@example.com"),
    expectedArg: { query: { email: "user@example.com" } },
  },
  {
    method: "requestMembership",
    op: "postV1IdentityMembershipRequests",
    invoke: (auth: AuthClient) => auth.requestMembership({ emailDomain: "example.com" }),
    expectedArg: { body: { emailDomain: "example.com" } },
  },
  {
    method: "validateRedirectUri",
    op: "getV1IdentityAuthRedirectUriValidate",
    invoke: (auth: AuthClient) => auth.validateRedirectUri("https://app.example.com/callback"),
    expectedArg: { query: { uri: "https://app.example.com/callback" } },
  },
];

/**
 * Build a facade instance for a test.
 *
 * Deliberately does NOT call `vi.resetModules()`. Resetting would re-evaluate
 * auth-client's whole module graph — including `./server/errors` — minting a
 * SECOND `WallowError` class per call, while this file's top-level
 * `import { WallowError }` stays bound to the FIRST graph. `instanceof` then
 * fails for any implementation, correct or not. There is no module-level state
 * to reset anyway: `createAuthClient()` is a pure factory over a stateless
 * object literal, and the only real module state — the `vi.mock("./generated")`
 * spy registry — is already cleared by `vi.clearAllMocks()` in each `beforeEach`.
 */
async function freshAuthClient(): Promise<AuthClient> {
  const mod = await import("./auth-client");
  return mod.createAuthClient();
}

describe("createAuthClient", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("builds a client when called with no options", async () => {
    const auth: AuthClient = await freshAuthClient();

    expect(auth).toBeDefined();
  });

  it("builds a client when called with an empty options object", async () => {
    const mod = await import("./auth-client");

    expect(mod.createAuthClient({})).toBeDefined();
  });

  it("exposes exactly the facade methods the auth app needs", async () => {
    const auth: AuthClient = await freshAuthClient();
    const expected: readonly string[] = [...new Set(METHOD_CASES.map((c: MethodCase) => c.method))];

    for (const method of expected) {
      expect(auth[method as keyof AuthClient]).toBeTypeOf("function");
    }
  });
});

describe.each(METHOD_CASES)("$method", ({ op, invoke, expectedArg }: MethodCase) => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it(`delegates to ${op} and returns the unwrapped data`, async () => {
    const data = { ok: true, marker: op };
    mocks[op].mockResolvedValue({ data });
    const auth: AuthClient = await freshAuthClient();

    const result: unknown = await invoke(auth);

    expect(mocks[op]).toHaveBeenCalledTimes(1);
    if (expectedArg === undefined) {
      expect(mocks[op]).toHaveBeenCalledWith();
    } else {
      expect(mocks[op]).toHaveBeenCalledWith(expectedArg);
    }
    expect(result).toBe(data);
  });

  it("throws a WallowError on the { error } envelope", async () => {
    mocks[op].mockResolvedValue({
      error: { status: 403, title: "Forbidden", detail: "Not allowed", code: "AUTH_FORBIDDEN" },
    });
    const auth: AuthClient = await freshAuthClient();

    await expect(invoke(auth)).rejects.toBeInstanceOf(WallowError);
  });
});

describe("envelope unwrapping", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("maps the problem details onto the thrown WallowError", async () => {
    mocks.postV1IdentityAuthLogin.mockResolvedValue({
      error: {
        status: 401,
        title: "Unauthorized",
        detail: "Bad credentials",
        code: "INVALID_CREDENTIALS",
      },
    });
    const auth: AuthClient = await freshAuthClient();

    const error: WallowError = await auth.login(LOGIN_BODY).then(
      () => {
        throw new Error("expected login to reject");
      },
      (thrown: unknown) => thrown as WallowError,
    );

    expect(error.status).toBe(401);
    expect(error.code).toBe("INVALID_CREDENTIALS");
    expect(error.title).toBe("Unauthorized");
    expect(error.detail).toBe("Bad credentials");
    expect(error.message).toBe("Unauthorized: Bad credentials");
  });

  it("reads the error code from extensions.code when it is not flattened", async () => {
    mocks.postV1IdentityAuthLogin.mockResolvedValue({
      error: { status: 400, title: "Bad Request", extensions: { code: "MFA_REQUIRED" } },
    });
    const auth: AuthClient = await freshAuthClient();

    const error = (await auth.login(LOGIN_BODY).catch((e: unknown) => e)) as WallowError;

    expect(error.code).toBe("MFA_REQUIRED");
  });

  it("falls back to UNKNOWN code and title when the error carries no problem details", async () => {
    mocks.postV1IdentityAuthLogin.mockResolvedValue({ error: {} });
    const auth: AuthClient = await freshAuthClient();

    const error = (await auth.login(LOGIN_BODY).catch((e: unknown) => e)) as WallowError;

    expect(error.code).toBe("UNKNOWN");
    expect(error.title).toBe("Unknown error");
  });

  it("falls back to the response status when the problem details omit one", async () => {
    mocks.postV1IdentityAuthLogin.mockResolvedValue({
      error: { title: "Server Error" },
      response: { status: 503 },
    });
    const auth: AuthClient = await freshAuthClient();

    const error = (await auth.login(LOGIN_BODY).catch((e: unknown) => e)) as WallowError;

    expect(error.status).toBe(503);
  });

  it("throws a WallowError even when the error body is a bare string", async () => {
    mocks.postV1IdentityAuthLogin.mockResolvedValue({ error: "boom" });
    const auth: AuthClient = await freshAuthClient();

    await expect(auth.login(LOGIN_BODY)).rejects.toBeInstanceOf(WallowError);
  });

  it("returns data untouched when the envelope carries no error", async () => {
    const data = { succeeded: true, signInTicket: "ticket-1" };
    mocks.postV1IdentityAuthLogin.mockResolvedValue({ data, error: undefined });
    const auth: AuthClient = await freshAuthClient();

    const result: unknown = await auth.login(LOGIN_BODY);

    expect(result).toBe(data);
  });

  it("returns a null data body rather than throwing", async () => {
    mocks.postV1IdentityAuthLogin.mockResolvedValue({ data: null });
    const auth: AuthClient = await freshAuthClient();

    const result: unknown = await auth.login(LOGIN_BODY);

    expect(result).toBeNull();
  });
});

describe("getConsentInfo scope encoding", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("comma-joins the requested scopes into a single query string", async () => {
    mocks.getV1IdentityAppsConsentInfoByClientId.mockResolvedValue({ data: {} });
    const auth: AuthClient = await freshAuthClient();

    await auth.getConsentInfo("client-1", ["openid", "profile", "email"]);

    expect(mocks.getV1IdentityAppsConsentInfoByClientId).toHaveBeenCalledWith({
      path: { clientId: "client-1" },
      query: { scopes: "openid,profile,email" },
    });
  });

  it("omits the scopes query entirely when no scopes are requested", async () => {
    mocks.getV1IdentityAppsConsentInfoByClientId.mockResolvedValue({ data: {} });
    const auth: AuthClient = await freshAuthClient();

    await auth.getConsentInfo("client-1");

    expect(mocks.getV1IdentityAppsConsentInfoByClientId).toHaveBeenCalledWith({
      path: { clientId: "client-1" },
    });
  });
});

describe("MFA verify op sharing", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("routes verifyMfa and useBackupCode through the one op, differing only in useBackupCode", async () => {
    mocks.postV1IdentityAuthMfaVerify.mockResolvedValue({ data: { succeeded: true } });
    const auth: AuthClient = await freshAuthClient();

    await auth.verifyMfa("111111");
    await auth.useBackupCode("recovery-code");

    expect(mocks.postV1IdentityAuthMfaVerify).toHaveBeenNthCalledWith(1, {
      body: { code: "111111", useBackupCode: false },
    });
    expect(mocks.postV1IdentityAuthMfaVerify).toHaveBeenNthCalledWith(2, {
      body: { code: "recovery-code", useBackupCode: true },
    });
  });
});
