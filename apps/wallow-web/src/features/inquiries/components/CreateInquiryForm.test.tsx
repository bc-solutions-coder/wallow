/** @vitest-environment jsdom */
import * as matchers from "@testing-library/jest-dom/matchers";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { CreateInquiryForm } from "./CreateInquiryForm";

// No global `expect` (vitest `globals` is off), so register the jest-dom
// matchers explicitly — the DOM-matcher convention for wallow-web's RTL tests
// (established by OrganizationList.test.tsx; Phases 4-6 copy it).
expect.extend(matchers);

/**
 * Component spec for the create-inquiry form (Wallow-8w1h.7.3). Copies the
 * CANONICAL CreateOrganizationForm.test.tsx shape (Wallow-8w1h.4.3): the
 * `getWallowSdk()` facade is mocked so the create call is a spy; the form
 * builds its mutation from `createInquiryMutation(queryClient)` (the api.ts
 * factory), so invalidation of `['inquiries']` on success is observed by
 * spying on the live client's `invalidateQueries`.
 *
 * Testids mirror the C# E2E `InquiryPage` page object verbatim: `inquiry-name`,
 * `inquiry-email`, `inquiry-phone`, `inquiry-company`, `inquiry-project-type`,
 * `inquiry-budget-range`, `inquiry-timeline`, `inquiry-message`,
 * `inquiry-submit`, `inquiry-success` / `inquiry-error`. Field-validation
 * messages use `{field}-error` (`inquiry-name-error`, etc.).
 */

// Hoisted so the vi.mock factory and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  list: vi.fn(),
  get: vi.fn(),
  create: vi.fn(),
  comments: vi.fn(),
  addComment: vi.fn(),
  setStatus: vi.fn(),
}));

// Mock the facade module the feature's api.ts imports (`../../lib/wallow-sdk`
// from features/inquiries/api.ts; `../../../lib/wallow-sdk` from this test file).
vi.mock("../../../lib/wallow-sdk", () => ({
  getWallowSdk: () => ({
    inquiries: {
      list: mocks.list,
      get: mocks.get,
      create: mocks.create,
      comments: mocks.comments,
      addComment: mocks.addComment,
      setStatus: mocks.setStatus,
    },
  }),
}));

// The full SubmitInquiryBody the form must POST when every field is filled.
const FULL_BODY = {
  name: "Ada Lovelace",
  email: "ada@example.com",
  phone: "555-0100",
  company: "Analytical Engines",
  projectType: "web-app",
  budgetRange: "15k-50k",
  timeline: "1-3-months",
  message: "We need a project dashboard.",
};

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

function renderWithClient(client: QueryClient, ui: ReactElement) {
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

type User = ReturnType<typeof userEvent.setup>;

/**
 * Per-field fill actions keyed by testid, so a test can fill the whole valid
 * form except a single field (`fillAllExcept`) to isolate that field's
 * required-validation behavior. `phone`/`company` are text inputs; the three
 * project selects use `selectOptions`.
 */
const FIELD_FILLERS: Record<string, (user: User) => Promise<void>> = {
  "inquiry-name": (user) => user.type(screen.getByTestId("inquiry-name"), FULL_BODY.name),
  "inquiry-email": (user) => user.type(screen.getByTestId("inquiry-email"), FULL_BODY.email),
  "inquiry-phone": (user) => user.type(screen.getByTestId("inquiry-phone"), FULL_BODY.phone),
  "inquiry-company": (user) => user.type(screen.getByTestId("inquiry-company"), FULL_BODY.company),
  "inquiry-project-type": (user) =>
    user.selectOptions(screen.getByTestId("inquiry-project-type"), FULL_BODY.projectType),
  "inquiry-budget-range": (user) =>
    user.selectOptions(screen.getByTestId("inquiry-budget-range"), FULL_BODY.budgetRange),
  "inquiry-timeline": (user) =>
    user.selectOptions(screen.getByTestId("inquiry-timeline"), FULL_BODY.timeline),
  "inquiry-message": (user) => user.type(screen.getByTestId("inquiry-message"), FULL_BODY.message),
};

async function fillFullForm(user: User) {
  for (const fill of Object.values(FIELD_FILLERS)) {
    await fill(user);
  }
}

/** Fill every field with a valid value except `skipTestId`, which is left blank. */
async function fillAllExcept(user: User, skipTestId: string) {
  for (const [testId, fill] of Object.entries(FIELD_FILLERS)) {
    if (testId !== skipTestId) {
      await fill(user);
    }
  }
}

/**
 * The fields `SubmitInquiryValidator.cs` marks `.NotEmpty()` beyond the core
 * name/email/message trio — phone plus the three project selects. Company is the
 * ONLY server-nullable field (`SubmitInquiryCommand.Company` is `string?`), so it
 * is deliberately excluded here. Each row is one required field + its
 * `{field}-error` testid.
 */
const SERVER_REQUIRED_SELECT_FIELDS = [
  { skipTestId: "inquiry-phone", errorTestId: "inquiry-phone-error" },
  { skipTestId: "inquiry-project-type", errorTestId: "inquiry-project-type-error" },
  { skipTestId: "inquiry-budget-range", errorTestId: "inquiry-budget-range-error" },
  { skipTestId: "inquiry-timeline", errorTestId: "inquiry-timeline-error" },
] as const;

describe("CreateInquiryForm", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders every inquiry field, select, and the submit button", () => {
    renderWithClient(newClient(), <CreateInquiryForm />);

    expect(screen.getByTestId("inquiry-name")).toBeInTheDocument();
    expect(screen.getByTestId("inquiry-email")).toBeInTheDocument();
    expect(screen.getByTestId("inquiry-phone")).toBeInTheDocument();
    expect(screen.getByTestId("inquiry-company")).toBeInTheDocument();
    expect(screen.getByTestId("inquiry-project-type")).toBeInTheDocument();
    expect(screen.getByTestId("inquiry-budget-range")).toBeInTheDocument();
    expect(screen.getByTestId("inquiry-timeline")).toBeInTheDocument();
    expect(screen.getByTestId("inquiry-message")).toBeInTheDocument();
    expect(screen.getByTestId("inquiry-submit")).toBeInTheDocument();
  });

  it("exposes the Blazor oracle's option values on each select", () => {
    renderWithClient(newClient(), <CreateInquiryForm />);

    const optionValues = (testId: string) =>
      [...screen.getByTestId(testId).querySelectorAll("option")].map(
        (o) => (o as HTMLOptionElement).value,
      );

    expect(optionValues("inquiry-project-type")).toEqual(
      expect.arrayContaining(["web-app", "mobile-app", "api", "saas", "consulting", "other"]),
    );
    expect(optionValues("inquiry-budget-range")).toEqual(
      expect.arrayContaining(["under-5k", "5k-15k", "15k-50k", "50k-100k", "over-100k"]),
    );
    expect(optionValues("inquiry-timeline")).toEqual(
      expect.arrayContaining(["asap", "1-3-months", "3-6-months", "6-plus-months", "flexible"]),
    );
  });

  it("submits, calling the create facade with the full SubmitInquiryBody", async () => {
    const user = userEvent.setup();
    mocks.create.mockResolvedValue({ id: "new", ...FULL_BODY, status: "New" });

    renderWithClient(newClient(), <CreateInquiryForm />);

    await fillFullForm(user);
    await user.click(screen.getByTestId("inquiry-submit"));

    await waitFor(() => {
      expect(mocks.create).toHaveBeenCalledTimes(1);
    });
    expect(mocks.create).toHaveBeenCalledWith(FULL_BODY);
  });

  it("invalidates the ['inquiries'] list query after a successful submit", async () => {
    const user = userEvent.setup();
    const client = newClient();
    const invalidateSpy = vi.spyOn(client, "invalidateQueries");
    mocks.create.mockResolvedValue({ id: "new", ...FULL_BODY, status: "New" });

    renderWithClient(client, <CreateInquiryForm />);

    await fillFullForm(user);
    await user.click(screen.getByTestId("inquiry-submit"));

    await waitFor(() => {
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["inquiries"] });
    });
  });

  it("shows the success state after a successful submit", async () => {
    const user = userEvent.setup();
    mocks.create.mockResolvedValue({ id: "new", ...FULL_BODY, status: "New" });

    renderWithClient(newClient(), <CreateInquiryForm />);

    await fillFullForm(user);
    await user.click(screen.getByTestId("inquiry-submit"));

    expect(await screen.findByTestId("inquiry-success")).toBeInTheDocument();
  });

  it("blocks submit and flags EVERY server-required field when the form is empty", async () => {
    // SubmitInquiryValidator.cs requires name, email, phone, projectType,
    // budgetRange, timeline, and message (all `.NotEmpty()`). Company is the only
    // nullable field. The client must mirror that contract so a user is never
    // told their submission is valid when the server will reject it.
    const user = userEvent.setup();

    renderWithClient(newClient(), <CreateInquiryForm />);

    await user.click(screen.getByTestId("inquiry-submit"));

    expect(await screen.findByTestId("inquiry-name-error")).toBeInTheDocument();
    expect(screen.getByTestId("inquiry-email-error")).toBeInTheDocument();
    expect(screen.getByTestId("inquiry-phone-error")).toBeInTheDocument();
    expect(screen.getByTestId("inquiry-project-type-error")).toBeInTheDocument();
    expect(screen.getByTestId("inquiry-budget-range-error")).toBeInTheDocument();
    expect(screen.getByTestId("inquiry-timeline-error")).toBeInTheDocument();
    expect(screen.getByTestId("inquiry-message-error")).toBeInTheDocument();
    expect(mocks.create).not.toHaveBeenCalled();
  });

  it("surfaces the RFC 7807 ProblemDetails detail when the submit fails", async () => {
    const user = userEvent.setup();
    mocks.create.mockRejectedValue({
      type: "https://httpstatuses.io/400",
      title: "Bad Request",
      status: "400",
      detail: "Failed to submit inquiry. Please try again.",
    });

    renderWithClient(newClient(), <CreateInquiryForm />);

    await fillFullForm(user);
    await user.click(screen.getByTestId("inquiry-submit"));

    const error = await screen.findByTestId("inquiry-error");
    expect(error).toHaveTextContent("Failed to submit inquiry. Please try again.");
  });
});

describe("CreateInquiryForm — server-required field parity", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it.each(SERVER_REQUIRED_SELECT_FIELDS)(
    "blocks submit and flags $errorTestId when only that server-required field is blank",
    async ({ skipTestId, errorTestId }) => {
      const user = userEvent.setup();
      mocks.create.mockResolvedValue({ id: "new", ...FULL_BODY, status: "New" });

      renderWithClient(newClient(), <CreateInquiryForm />);

      await fillAllExcept(user, skipTestId);
      await user.click(screen.getByTestId("inquiry-submit"));

      expect(await screen.findByTestId(errorTestId)).toBeInTheDocument();
      expect(mocks.create).not.toHaveBeenCalled();
    },
  );

  it("still submits when only company (the sole server-optional field) is blank", async () => {
    const user = userEvent.setup();
    mocks.create.mockResolvedValue({ id: "new", ...FULL_BODY, company: "", status: "New" });

    renderWithClient(newClient(), <CreateInquiryForm />);

    await fillAllExcept(user, "inquiry-company");
    await user.click(screen.getByTestId("inquiry-submit"));

    await waitFor(() => {
      expect(mocks.create).toHaveBeenCalledTimes(1);
    });
    expect(mocks.create).toHaveBeenCalledWith({ ...FULL_BODY, company: "" });
  });
});
