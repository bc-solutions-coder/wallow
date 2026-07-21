import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { RegisterAppForm } from "./RegisterAppForm";

/**
 * Component spec for the register-app form (Wallow-8w1h.5.3). Copies the
 * CANONICAL create-form template (CreateOrganizationForm) — `useForm` (TanStack
 * Form) + `useMutation(registerAppMutation(queryClient))` — and adds the
 * behaviors unique to app registration:
 *
 *   - Blazor field remap (RegisterApp.razor / AppRegistrationService.cs):
 *     DisplayName -> clientName, Scopes -> requestedScopes; clientType defaults
 *     to "public"; redirect URIs are a newline-separated textarea split on `\n`
 *     with empty lines dropped.
 *   - Scope multi-select toggle buttons (available: inquiries.read,
 *     inquiries.write, announcements.read, storage.read; default selected:
 *     inquiries.read).
 *   - The ONE-TIME client secret: `AppRegistrationResponse.clientSecret` is
 *     returned ONLY from the register call (GET /apps and GET /apps/{id} carry
 *     no secret), so it is rendered exactly once — after a successful
 *     registration — via `data-testid=app-client-secret` (+ `app-client-id`),
 *     never before, and never re-fetchable. Mirrors RegisterAppResult.cs /
 *     RegisterApp.razor:37-49 ("Save your client secret now. It will not be
 *     shown again.").
 *
 * The `getWallowSdk()` facade is mocked so `apps.register` is a spy; the form
 * builds its mutation from `registerAppMutation(queryClient)` (the api.ts
 * factory), so invalidation of `['apps']` on success is observed by spying on
 * the live client's `invalidateQueries`.
 *
 * Testids follow the apps feature's `app-*` convention (like `app-item`, and
 * the bead-mandated `app-client-secret`/`app-client-id`): `app-display-name`
 * (input), `app-client-type` (select), `app-redirect-uris` (textarea),
 * `app-scope-{scope-dashed}` (toggle buttons), `app-register-submit` (submit),
 * `app-display-name-error` (required-field validation), `app-register-error`
 * (server RFC 7807 ProblemDetails surface), `app-client-secret` +
 * `app-client-secret-copy` + `app-client-id` (one-time success reveal).
 */

// Hoisted so the vi.mock factory and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  list: vi.fn(),
  get: vi.fn(),
  register: vi.fn(),
}));

// Mock the facade module the feature's api.ts imports (`../../lib/wallow-sdk`
// from features/apps; `../../../lib/wallow-sdk` from this test file).
vi.mock("../../../lib/wallow-sdk", () => ({
  getWallowSdk: () => ({
    apps: { list: mocks.list, get: mocks.get, register: mocks.register },
  }),
}));

const OK_RESPONSE = {
  clientId: "client-abc",
  clientSecret: "secret-xyz",
  registrationAccessToken: "rat-123",
};

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

function renderWithClient(client: QueryClient, ui: ReactElement) {
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("RegisterAppForm", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders the display-name, client-type, redirect-uris, scope, and submit controls", async () => {
    renderWithClient(newClient(), <RegisterAppForm />);

    await expect.element(page.getByTestId("app-display-name")).toBeInTheDocument();
    await expect.element(page.getByTestId("app-client-type")).toBeInTheDocument();
    await expect.element(page.getByTestId("app-redirect-uris")).toBeInTheDocument();
    await expect.element(page.getByTestId("app-scope-inquiries-read")).toBeInTheDocument();
    await expect.element(page.getByTestId("app-scope-announcements-read")).toBeInTheDocument();
    await expect.element(page.getByTestId("app-register-submit")).toBeInTheDocument();
  });

  it("does NOT reveal the client secret or client id before a successful registration", async () => {
    renderWithClient(newClient(), <RegisterAppForm />);

    await expect.element(page.getByTestId("app-client-secret")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("app-client-id")).not.toBeInTheDocument();
  });

  it("submits, calling register with the remapped body (clientName, default scope, public, parsed URIs)", async () => {
    mocks.register.mockResolvedValue(OK_RESPONSE);

    renderWithClient(newClient(), <RegisterAppForm />);

    await userEvent.type(page.getByTestId("app-display-name"), "My App");
    await userEvent.type(
      page.getByTestId("app-redirect-uris"),
      "https://a.com/cb{enter}https://b.com/cb",
    );
    await userEvent.click(page.getByTestId("app-register-submit"));

    await vi.waitFor(() => {
      expect(mocks.register).toHaveBeenCalledTimes(1);
    });
    expect(mocks.register).toHaveBeenCalledWith({
      clientName: "My App",
      requestedScopes: ["inquiries.read"],
      clientType: "public",
      redirectUris: ["https://a.com/cb", "https://b.com/cb"],
    });
  });

  it("adds a toggled-on scope to the submitted requestedScopes", async () => {
    mocks.register.mockResolvedValue(OK_RESPONSE);

    renderWithClient(newClient(), <RegisterAppForm />);

    await userEvent.type(page.getByTestId("app-display-name"), "My App");
    await userEvent.click(page.getByTestId("app-scope-announcements-read"));
    await userEvent.click(page.getByTestId("app-register-submit"));

    await vi.waitFor(() => {
      expect(mocks.register).toHaveBeenCalledTimes(1);
    });
    const body = mocks.register.mock.calls[0]![0] as { requestedScopes: string[] };
    expect(body.requestedScopes).toEqual(
      expect.arrayContaining(["inquiries.read", "announcements.read"]),
    );
    expect(body.requestedScopes).toHaveLength(2);
  });

  it("removes a default scope when its toggle is clicked off", async () => {
    mocks.register.mockResolvedValue(OK_RESPONSE);

    renderWithClient(newClient(), <RegisterAppForm />);

    await userEvent.type(page.getByTestId("app-display-name"), "My App");
    await userEvent.click(page.getByTestId("app-scope-inquiries-read"));
    await userEvent.click(page.getByTestId("app-register-submit"));

    await vi.waitFor(() => {
      expect(mocks.register).toHaveBeenCalledTimes(1);
    });
    const body = mocks.register.mock.calls[0]![0] as { requestedScopes: string[] };
    expect(body.requestedScopes).not.toContain("inquiries.read");
  });

  it("reveals the one-time client secret and client id after a successful registration", async () => {
    mocks.register.mockResolvedValue(OK_RESPONSE);

    renderWithClient(newClient(), <RegisterAppForm />);

    await userEvent.type(page.getByTestId("app-display-name"), "My App");
    await userEvent.click(page.getByTestId("app-register-submit"));

    await expect.element(page.getByTestId("app-client-secret")).toHaveTextContent("secret-xyz");
    await expect.element(page.getByTestId("app-client-id")).toHaveTextContent("client-abc");
  });

  it("copies the revealed client secret to the clipboard", async () => {
    mocks.register.mockResolvedValue(OK_RESPONSE);
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", {
      value: { writeText },
      configurable: true,
    });

    renderWithClient(newClient(), <RegisterAppForm />);

    await userEvent.type(page.getByTestId("app-display-name"), "My App");
    await userEvent.click(page.getByTestId("app-register-submit"));

    await expect.element(page.getByTestId("app-client-secret")).toBeInTheDocument();
    await userEvent.click(page.getByTestId("app-client-secret-copy"));

    expect(writeText).toHaveBeenCalledWith("secret-xyz");
  });

  it("invalidates the ['apps'] list query after a successful registration", async () => {
    const client = newClient();
    const invalidateSpy = vi.spyOn(client, "invalidateQueries");
    mocks.register.mockResolvedValue(OK_RESPONSE);

    renderWithClient(client, <RegisterAppForm />);

    await userEvent.type(page.getByTestId("app-display-name"), "My App");
    await userEvent.click(page.getByTestId("app-register-submit"));

    await vi.waitFor(() => {
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["apps"] });
    });
  });

  it("blocks submit and shows a required error when the display name is empty", async () => {
    renderWithClient(newClient(), <RegisterAppForm />);

    await userEvent.click(page.getByTestId("app-register-submit"));

    await expect.element(page.getByTestId("app-display-name-error")).toBeInTheDocument();
    expect(mocks.register).not.toHaveBeenCalled();
  });

  it("surfaces the ProblemDetails detail when registration fails", async () => {
    mocks.register.mockRejectedValue({
      type: "https://httpstatuses.io/400",
      title: "Bad Request",
      status: "400",
      detail: "That redirect URI is not allowed.",
    });

    renderWithClient(newClient(), <RegisterAppForm />);

    await userEvent.type(page.getByTestId("app-display-name"), "My App");
    await userEvent.click(page.getByTestId("app-register-submit"));

    await expect
      .element(page.getByTestId("app-register-error"))
      .toHaveTextContent("That redirect URI is not allowed.");
  });
});
