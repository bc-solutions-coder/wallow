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
  getV1IdentityOrganizations: vi.fn(),
  getV1IdentityOrganizationsById: vi.fn(),
  postV1IdentityOrganizations: vi.fn(),
  getV1IdentityOrganizationsByIdMembers: vi.fn(),
  postV1IdentityOrganizationsByIdMembers: vi.fn(),
  deleteV1IdentityOrganizationsByIdMembersByUserId: vi.fn(),
  postV1IdentityOrganizationsByIdArchive: vi.fn(),
  postV1IdentityOrganizationsByIdReactivate: vi.fn(),
  getUser: vi.fn(),
  client: { interceptors: { request: { use: vi.fn() } } },
}));

vi.mock("@bc-solutions-coder/sdk", () => ({
  configureBffClient: mocks.configureBffClient,
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
  getUser: mocks.getUser,
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

  describe("singleton configuration", () => {
    it("configures the BFF client exactly once across multiple calls", async () => {
      const getWallowSdk = await freshFacade();

      getWallowSdk();
      getWallowSdk();
      getWallowSdk();

      expect(mocks.configureBffClient).toHaveBeenCalledTimes(1);
    });

    it("wires the CSRF interceptor onto the shared client on first use", async () => {
      const getWallowSdk = await freshFacade();

      getWallowSdk();

      expect(mocks.client.interceptors.request.use).toHaveBeenCalledTimes(1);
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
});
