import { beforeEach, describe, expect, it, vi } from "vitest";

/**
 * getWallowSdk() facade (Wallow-8w1h.3.3). The single guarded-singleton entry
 * every feature (Phases 3-6) extends. On first use it configures the BFF client
 * exactly once and wires the CSRF request interceptor onto the shared `@hey-api`
 * client; thereafter it returns a namespaced object whose slice methods delegate
 * to the SDK's generated ops and unwrap `{ data, error }` — returning `data` on
 * success and THROWING the `error` (RFC 7807 ProblemDetails) so React Query
 * surfaces it, never returning `undefined`.
 *
 * The generated ops are mocked here because this facade is the ONLY module
 * permitted to import them; the tests assert delegation, not the wire.
 */

// Hoisted so the vi.mock factory and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  configureBffClient: vi.fn(),
  configureSsrClient: vi.fn(),
  getSsrRequestContext: vi.fn(() => undefined),
  getV1IdentityOrganizations: vi.fn(),
  getV1IdentityOrganizationsById: vi.fn(),
  postV1IdentityOrganizations: vi.fn(),
  getV1IdentityOrganizationsByIdMembers: vi.fn(),
  postV1IdentityOrganizationsByIdMembers: vi.fn(),
  deleteV1IdentityOrganizationsByIdMembersByUserId: vi.fn(),
  postV1IdentityOrganizationsByIdArchive: vi.fn(),
  postV1IdentityOrganizationsByIdReactivate: vi.fn(),
  getV1IdentityApps: vi.fn(),
  getV1IdentityAppsByClientId: vi.fn(),
  postV1IdentityAppsRegister: vi.fn(),
  getUser: vi.fn(),
  getV1IdentityUsersMe: vi.fn(),
  getV1IdentityMfaStatus: vi.fn(),
  postV1IdentityMfaEnrollTotp: vi.fn(),
  postV1IdentityMfaEnrollConfirm: vi.fn(),
  postV1IdentityMfaDisable: vi.fn(),
  postV1IdentityMfaBackupCodesRegenerate: vi.fn(),
  getV1InquiriesSubmitted: vi.fn(),
  postV1Inquiries: vi.fn(),
  getV1InquiriesById: vi.fn(),
  getV1InquiriesByIdComments: vi.fn(),
  postV1InquiriesByIdComments: vi.fn(),
  patchV1InquiriesByIdStatus: vi.fn(),
  client: { interceptors: { request: { use: vi.fn() } } },
}));

vi.mock("@bc-solutions-coder/sdk", () => ({
  // Real (passthrough) facade helpers: the collapsed facade imports unwrap +
  // createConfiguredOnce from the SDK, so the mock must supply working
  // implementations. createConfiguredOnce returns a fresh lazy singleton per
  // module graph, which is exactly what freshFacade()'s vi.resetModules wants.
  unwrap: async <T>(pending: Promise<{ data?: T; error?: unknown }>): Promise<T> => {
    const { data, error } = await pending;
    if (error !== undefined) {
      throw error;
    }
    return data as T;
  },
  createConfiguredOnce: <TFacade>(configure: () => void, build: () => TFacade): (() => TFacade) => {
    let facade: TFacade | undefined;
    let ready = false;
    return (): TFacade => {
      if (!ready) {
        configure();
        facade = build();
        ready = true;
      }
      return facade as TFacade;
    };
  },
  configureBffClient: mocks.configureBffClient,
  configureSsrClient: mocks.configureSsrClient,
  getSsrRequestContext: mocks.getSsrRequestContext,
  client: mocks.client,
  getV1IdentityOrganizations: mocks.getV1IdentityOrganizations,
  getV1IdentityOrganizationsById: mocks.getV1IdentityOrganizationsById,
  postV1IdentityOrganizations: mocks.postV1IdentityOrganizations,
  getV1IdentityOrganizationsByIdMembers: mocks.getV1IdentityOrganizationsByIdMembers,
  postV1IdentityOrganizationsByIdMembers: mocks.postV1IdentityOrganizationsByIdMembers,
  deleteV1IdentityOrganizationsByIdMembersByUserId:
    mocks.deleteV1IdentityOrganizationsByIdMembersByUserId,
  postV1IdentityOrganizationsByIdArchive: mocks.postV1IdentityOrganizationsByIdArchive,
  postV1IdentityOrganizationsByIdReactivate: mocks.postV1IdentityOrganizationsByIdReactivate,
  getV1IdentityApps: mocks.getV1IdentityApps,
  getV1IdentityAppsByClientId: mocks.getV1IdentityAppsByClientId,
  postV1IdentityAppsRegister: mocks.postV1IdentityAppsRegister,
  getUser: mocks.getUser,
  getV1IdentityUsersMe: mocks.getV1IdentityUsersMe,
  getV1IdentityMfaStatus: mocks.getV1IdentityMfaStatus,
  postV1IdentityMfaEnrollTotp: mocks.postV1IdentityMfaEnrollTotp,
  postV1IdentityMfaEnrollConfirm: mocks.postV1IdentityMfaEnrollConfirm,
  postV1IdentityMfaDisable: mocks.postV1IdentityMfaDisable,
  postV1IdentityMfaBackupCodesRegenerate: mocks.postV1IdentityMfaBackupCodesRegenerate,
  getV1InquiriesSubmitted: mocks.getV1InquiriesSubmitted,
  postV1Inquiries: mocks.postV1Inquiries,
  getV1InquiriesById: mocks.getV1InquiriesById,
  getV1InquiriesByIdComments: mocks.getV1InquiriesByIdComments,
  postV1InquiriesByIdComments: mocks.postV1InquiriesByIdComments,
  patchV1InquiriesByIdStatus: mocks.patchV1InquiriesByIdStatus,
}));

/**
 * Re-evaluate the facade module so its `configured` singleton flag starts
 * fresh, then hand back `getWallowSdk`. Each test drives a clean singleton.
 */
async function freshFacade(): Promise<typeof import("./wallow-sdk").getWallowSdk> {
  vi.resetModules();
  const mod = await import("./wallow-sdk");
  return mod.getWallowSdk;
}

describe("getWallowSdk", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  /**
   * Singleton configuration (Wallow-0q2s.7.2). This vitest node project runs with
   * `import.meta.env.SSR === true`, so `ensureConfigured()` takes the SSR branch:
   * it reads the request context from the SDK's relocated
   * `getSsrRequestContext()` seam and delegates all client wiring (absolute-origin
   * baseUrl + the live cookie-forwarding interceptor) to the SDK's
   * `configureSsrClient()`. The SSR wiring no longer lives in this app — it moved
   * into `@bc-solutions-coder/sdk`, so the facade never hand-rolls the interceptor
   * (`configureBffClient` + the CSRF path apply only in the browser branch).
   */
  describe("singleton configuration", () => {
    it("configures the SSR client exactly once across multiple calls", async () => {
      const getWallowSdk = await freshFacade();

      getWallowSdk();
      getWallowSdk();
      getWallowSdk();

      expect(mocks.configureSsrClient).toHaveBeenCalledTimes(1);
    });

    it("reads the SSR request context and delegates client wiring to configureSsrClient", async () => {
      const getWallowSdk = await freshFacade();

      getWallowSdk();

      expect(mocks.getSsrRequestContext).toHaveBeenCalled();
      expect(mocks.configureSsrClient).toHaveBeenCalledTimes(1);
      // The SSR branch delegates entirely to the SDK helper; it does not fall
      // through to the browser-only configureBffClient path.
      expect(mocks.configureBffClient).not.toHaveBeenCalled();
    });
  });

  describe("organizations slice", () => {
    it("list() delegates to getV1IdentityOrganizations and returns data", async () => {
      const orgs = [{ id: "o1", name: "Acme" }];
      mocks.getV1IdentityOrganizations.mockResolvedValue({ data: orgs });
      const getWallowSdk = await freshFacade();

      const result = await getWallowSdk().organizations.list();

      expect(mocks.getV1IdentityOrganizations).toHaveBeenCalledTimes(1);
      expect(result).toBe(orgs);
    });

    it("get(id) delegates to getV1IdentityOrganizationsById with the path id", async () => {
      const org = { id: "o1", name: "Acme" };
      mocks.getV1IdentityOrganizationsById.mockResolvedValue({ data: org });
      const getWallowSdk = await freshFacade();

      const result = await getWallowSdk().organizations.get("o1");

      expect(mocks.getV1IdentityOrganizationsById).toHaveBeenCalledWith({
        path: { id: "o1" },
      });
      expect(result).toBe(org);
    });

    it("create(body) delegates to postV1IdentityOrganizations with the body", async () => {
      const created = { id: "o2", name: "New" };
      mocks.postV1IdentityOrganizations.mockResolvedValue({ data: created });
      const getWallowSdk = await freshFacade();
      const body = { name: "New", domain: null };

      const result = await getWallowSdk().organizations.create(body);

      expect(mocks.postV1IdentityOrganizations).toHaveBeenCalledWith({ body });
      expect(result).toBe(created);
    });

    it("throws the ProblemDetails on { error } instead of returning undefined", async () => {
      const problem = { status: 403, title: "Forbidden", errorCode: "CSRF_INVALID" };
      mocks.getV1IdentityOrganizations.mockResolvedValue({ error: problem });
      const getWallowSdk = await freshFacade();

      await expect(getWallowSdk().organizations.list()).rejects.toBe(problem);
    });
  });

  describe("organizations slice — members & lifecycle (Wallow-8w1h.4.4)", () => {
    it("members(id) delegates to getV1IdentityOrganizationsByIdMembers with the path id", async () => {
      const members = [{ id: "u1", email: "a@b.c", roles: ["Owner"] }];
      mocks.getV1IdentityOrganizationsByIdMembers.mockResolvedValue({ data: members });
      const getWallowSdk = await freshFacade();

      const result = await getWallowSdk().organizations.members("o1");

      expect(mocks.getV1IdentityOrganizationsByIdMembers).toHaveBeenCalledWith({
        path: { id: "o1" },
      });
      expect(result).toBe(members);
    });

    it("addMember(id, body) delegates to postV1IdentityOrganizationsByIdMembers with path + body", async () => {
      const added = { id: "u2" };
      mocks.postV1IdentityOrganizationsByIdMembers.mockResolvedValue({ data: added });
      const getWallowSdk = await freshFacade();

      const result = await getWallowSdk().organizations.addMember("o1", { userId: "u2" });

      expect(mocks.postV1IdentityOrganizationsByIdMembers).toHaveBeenCalledWith({
        path: { id: "o1" },
        body: { userId: "u2" },
      });
      expect(result).toBe(added);
    });

    it("removeMember(id, userId) delegates to deleteV1IdentityOrganizationsByIdMembersByUserId", async () => {
      mocks.deleteV1IdentityOrganizationsByIdMembersByUserId.mockResolvedValue({ data: undefined });
      const getWallowSdk = await freshFacade();

      await getWallowSdk().organizations.removeMember("o1", "u2");

      expect(mocks.deleteV1IdentityOrganizationsByIdMembersByUserId).toHaveBeenCalledWith({
        path: { id: "o1", userId: "u2" },
      });
    });

    it("archive(id) delegates to postV1IdentityOrganizationsByIdArchive with the path id", async () => {
      mocks.postV1IdentityOrganizationsByIdArchive.mockResolvedValue({ data: undefined });
      const getWallowSdk = await freshFacade();

      await getWallowSdk().organizations.archive("o1");

      expect(mocks.postV1IdentityOrganizationsByIdArchive).toHaveBeenCalledWith({
        path: { id: "o1" },
      });
    });

    it("reactivate(id) delegates to postV1IdentityOrganizationsByIdReactivate with the path id", async () => {
      mocks.postV1IdentityOrganizationsByIdReactivate.mockResolvedValue({ data: undefined });
      const getWallowSdk = await freshFacade();

      await getWallowSdk().organizations.reactivate("o1");

      expect(mocks.postV1IdentityOrganizationsByIdReactivate).toHaveBeenCalledWith({
        path: { id: "o1" },
      });
    });
  });

  describe("apps slice (Wallow-8w1h.5.1)", () => {
    it("list() delegates to getV1IdentityApps and returns data", async () => {
      const apps = [{ clientId: "c1", displayName: "My App" }];
      mocks.getV1IdentityApps.mockResolvedValue({ data: apps });
      const getWallowSdk = await freshFacade();

      const result = await getWallowSdk().apps.list();

      expect(mocks.getV1IdentityApps).toHaveBeenCalledTimes(1);
      expect(result).toBe(apps);
    });

    it("get(clientId) delegates to getV1IdentityAppsByClientId with the path clientId", async () => {
      const app = { clientId: "c1", displayName: "My App" };
      mocks.getV1IdentityAppsByClientId.mockResolvedValue({ data: app });
      const getWallowSdk = await freshFacade();

      const result = await getWallowSdk().apps.get("c1");

      expect(mocks.getV1IdentityAppsByClientId).toHaveBeenCalledWith({
        path: { clientId: "c1" },
      });
      expect(result).toBe(app);
    });

    it("register(body) delegates to postV1IdentityAppsRegister with the body", async () => {
      const registered = {
        clientId: "c2",
        clientSecret: "s3cr3t",
        registrationAccessToken: "rat",
      };
      mocks.postV1IdentityAppsRegister.mockResolvedValue({ data: registered });
      const getWallowSdk = await freshFacade();
      const body = {
        clientName: "New App",
        requestedScopes: ["inquiries.read"],
        clientType: "public",
        redirectUris: null,
      };

      const result = await getWallowSdk().apps.register(body);

      expect(mocks.postV1IdentityAppsRegister).toHaveBeenCalledWith({ body });
      expect(result).toBe(registered);
    });

    it("throws the ProblemDetails on { error } instead of returning undefined", async () => {
      const problem = { status: 403, title: "Forbidden", errorCode: "CSRF_INVALID" };
      mocks.getV1IdentityApps.mockResolvedValue({ error: problem });
      const getWallowSdk = await freshFacade();

      await expect(getWallowSdk().apps.list()).rejects.toBe(problem);
    });
  });

  describe("user slice", () => {
    it("me() delegates to the SDK getUser()", async () => {
      const user = { id: "u1", email: "a@b.c" };
      mocks.getUser.mockResolvedValue(user);
      const getWallowSdk = await freshFacade();

      const result = await getWallowSdk().user.me();

      expect(mocks.getUser).toHaveBeenCalledTimes(1);
      expect(result).toBe(user);
    });
  });

  /**
   * Settings slice (Wallow-8w1h.6.1). RECONCILIATION: per the scout's CRITICAL
   * DIVERGENCE #1, profile is READ-ONLY and sourced from getV1IdentityUsersMe
   * (CurrentUserResponse), NOT the generic key/value settings endpoints
   * getV1IdentitySettingsUser/putV1IdentitySettingsUser. No profile mutation
   * exists, so the facade exposes only getProfile().
   */
  describe("settings slice (Wallow-8w1h.6.1)", () => {
    it("getProfile() delegates to getV1IdentityUsersMe and returns data", async () => {
      const profile = {
        id: "u1",
        email: "a@b.c",
        firstName: "Ada",
        lastName: "Lovelace",
        roles: ["Owner"],
        permissions: [],
      };
      mocks.getV1IdentityUsersMe.mockResolvedValue({ data: profile });
      const getWallowSdk = await freshFacade();

      const result = await getWallowSdk().settings.getProfile();

      expect(mocks.getV1IdentityUsersMe).toHaveBeenCalledTimes(1);
      expect(result).toBe(profile);
    });

    it("throws the ProblemDetails on { error } instead of returning undefined", async () => {
      const problem = { status: 403, title: "Forbidden", errorCode: "CSRF_INVALID" };
      mocks.getV1IdentityUsersMe.mockResolvedValue({ error: problem });
      const getWallowSdk = await freshFacade();

      await expect(getWallowSdk().settings.getProfile()).rejects.toBe(problem);
    });
  });

  /**
   * MFA slice (Wallow-8w1h.6.3). The generated MFA ops resolve `unknown` bodies
   * (untyped-response gap), so the slice returns `Promise<unknown>` and the
   * feature narrows via local types. These tests assert delegation + the
   * SDK-accurate request shapes (confirm sends `{ secret, code }`; disable and
   * regenerate send `{ password }`), which the terse bead DESIGN omitted.
   */
  describe("mfa slice (Wallow-8w1h.6.3)", () => {
    it("status() delegates to getV1IdentityMfaStatus and returns data", async () => {
      const status = { enabled: true, method: "totp", backupCodeCount: 8 };
      mocks.getV1IdentityMfaStatus.mockResolvedValue({ data: status });
      const getWallowSdk = await freshFacade();

      const result = await getWallowSdk().mfa.status();

      expect(mocks.getV1IdentityMfaStatus).toHaveBeenCalledTimes(1);
      expect(result).toBe(status);
    });

    it("enrollTotp() delegates to postV1IdentityMfaEnrollTotp and returns data", async () => {
      const enroll = { secret: "ABC", qrUri: "otpauth://totp/x" };
      mocks.postV1IdentityMfaEnrollTotp.mockResolvedValue({ data: enroll });
      const getWallowSdk = await freshFacade();

      const result = await getWallowSdk().mfa.enrollTotp();

      expect(mocks.postV1IdentityMfaEnrollTotp).toHaveBeenCalledTimes(1);
      expect(result).toBe(enroll);
    });

    it("confirmEnroll(secret, code) delegates to postV1IdentityMfaEnrollConfirm with the { secret, code } body", async () => {
      const confirmed = { succeeded: true, backupCodes: ["a", "b"] };
      mocks.postV1IdentityMfaEnrollConfirm.mockResolvedValue({ data: confirmed });
      const getWallowSdk = await freshFacade();

      const result = await getWallowSdk().mfa.confirmEnroll("ABC", "123456");

      expect(mocks.postV1IdentityMfaEnrollConfirm).toHaveBeenCalledWith({
        body: { secret: "ABC", code: "123456" },
      });
      expect(result).toBe(confirmed);
    });

    it("disable(password) delegates to postV1IdentityMfaDisable with the { password } body", async () => {
      const disabled = { succeeded: true };
      mocks.postV1IdentityMfaDisable.mockResolvedValue({ data: disabled });
      const getWallowSdk = await freshFacade();

      const result = await getWallowSdk().mfa.disable("hunter2");

      expect(mocks.postV1IdentityMfaDisable).toHaveBeenCalledWith({
        body: { password: "hunter2" },
      });
      expect(result).toBe(disabled);
    });

    it("regenerateBackupCodes(password) delegates to postV1IdentityMfaBackupCodesRegenerate with the { password } body", async () => {
      const regenerated = { codes: ["x", "y", "z"] };
      mocks.postV1IdentityMfaBackupCodesRegenerate.mockResolvedValue({ data: regenerated });
      const getWallowSdk = await freshFacade();

      const result = await getWallowSdk().mfa.regenerateBackupCodes("hunter2");

      expect(mocks.postV1IdentityMfaBackupCodesRegenerate).toHaveBeenCalledWith({
        body: { password: "hunter2" },
      });
      expect(result).toBe(regenerated);
    });

    it("throws the ProblemDetails on { error } instead of returning undefined", async () => {
      const problem = { status: 403, title: "Forbidden", errorCode: "CSRF_INVALID" };
      mocks.getV1IdentityMfaStatus.mockResolvedValue({ error: problem });
      const getWallowSdk = await freshFacade();

      await expect(getWallowSdk().mfa.status()).rejects.toBe(problem);
    });
  });

  /**
   * Inquiries slice (Wallow-8w1h.7.1). Asserts delegation + SDK-accurate mapping:
   * `list()` -> getV1InquiriesSubmitted (the caller's own inquiries, NOT the admin
   * all-view); `setStatus()` sends `{ newStatus }` (the field is `newStatus`, not
   * `status` as the bead DESIGN said). The comment-POST 201 body is untyped, so
   * `addComment()` still delegates but its result is `unknown`.
   */
  describe("inquiries slice (Wallow-8w1h.7.1)", () => {
    it("list() delegates to getV1InquiriesSubmitted and returns data", async () => {
      const inquiries = [{ id: "i1", name: "Ada", status: "New" }];
      mocks.getV1InquiriesSubmitted.mockResolvedValue({ data: inquiries });
      const getWallowSdk = await freshFacade();

      const result = await getWallowSdk().inquiries.list();

      expect(mocks.getV1InquiriesSubmitted).toHaveBeenCalledTimes(1);
      expect(result).toBe(inquiries);
    });

    it("create(body) delegates to postV1Inquiries with the body", async () => {
      const created = { id: "i2", name: "Grace", status: "New" };
      mocks.postV1Inquiries.mockResolvedValue({ data: created });
      const getWallowSdk = await freshFacade();
      const body = {
        name: "Grace",
        email: "grace@example.com",
        phone: "555-0100",
        company: null,
        projectType: "web-app",
        budgetRange: "5k-15k",
        timeline: "asap",
        message: "Hello",
      };

      const result = await getWallowSdk().inquiries.create(body);

      expect(mocks.postV1Inquiries).toHaveBeenCalledWith({ body });
      expect(result).toBe(created);
    });

    it("get(id) delegates to getV1InquiriesById with the path id", async () => {
      const inquiry = { id: "i1", name: "Ada", status: "New" };
      mocks.getV1InquiriesById.mockResolvedValue({ data: inquiry });
      const getWallowSdk = await freshFacade();

      const result = await getWallowSdk().inquiries.get("i1");

      expect(mocks.getV1InquiriesById).toHaveBeenCalledWith({ path: { id: "i1" } });
      expect(result).toBe(inquiry);
    });

    it("comments(id) delegates to getV1InquiriesByIdComments with the path id", async () => {
      const comments = [{ id: "c1", inquiryId: "i1", content: "hi", isInternal: false }];
      mocks.getV1InquiriesByIdComments.mockResolvedValue({ data: comments });
      const getWallowSdk = await freshFacade();

      const result = await getWallowSdk().inquiries.comments("i1");

      expect(mocks.getV1InquiriesByIdComments).toHaveBeenCalledWith({ path: { id: "i1" } });
      expect(result).toBe(comments);
    });

    it("addComment(id, body) delegates to postV1InquiriesByIdComments with path + body", async () => {
      mocks.postV1InquiriesByIdComments.mockResolvedValue({ data: undefined });
      const getWallowSdk = await freshFacade();
      const body = { content: "Following up", isInternal: true };

      await getWallowSdk().inquiries.addComment("i1", body);

      expect(mocks.postV1InquiriesByIdComments).toHaveBeenCalledWith({
        path: { id: "i1" },
        body,
      });
    });

    it("setStatus(id, newStatus) delegates to patchV1InquiriesByIdStatus with { newStatus } body", async () => {
      const updated = { id: "i1", name: "Ada", status: "Reviewed" };
      mocks.patchV1InquiriesByIdStatus.mockResolvedValue({ data: updated });
      const getWallowSdk = await freshFacade();

      const result = await getWallowSdk().inquiries.setStatus("i1", "Reviewed");

      expect(mocks.patchV1InquiriesByIdStatus).toHaveBeenCalledWith({
        path: { id: "i1" },
        body: { newStatus: "Reviewed" },
      });
      expect(result).toBe(updated);
    });

    it("throws the ProblemDetails on { error } instead of returning undefined", async () => {
      const problem = { status: 404, title: "Not Found", errorCode: "INQUIRY_NOT_FOUND" };
      mocks.getV1InquiriesById.mockResolvedValue({ error: problem });
      const getWallowSdk = await freshFacade();

      await expect(getWallowSdk().inquiries.get("missing")).rejects.toBe(problem);
    });
  });
});
