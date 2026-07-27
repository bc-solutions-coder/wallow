import { render } from "@bc-solutions-coder/testing/render";
import { type ReactElement, useState } from "react";
import { describe, expect, it } from "vitest";
import { userEvent } from "vitest/browser";

import { createToastManager, Toast, useToastManager } from "./toast";

/*
 * Toast behavioural spec (Wallow-m5aq.3.11), shaped after the Wallow-m5aq.3.1
 * Dialog EXEMPLAR:
 *
 *   1. Runs in the vitest BROWSER project — real headless Chromium, real Base UI,
 *      real DOM, real timers. Nothing is mocked.
 *   2. Recipes are asserted THROUGH the component, never by importing
 *      `toastRootRecipe` and inspecting its return value.
 *   3. Class assertions are an ORDER-FREE SET (`classSet`), because
 *      `cn()`/tailwind-merge is free to reorder. The `*_CLASSES` constants below
 *      are the single source of truth for what each recipe must contain — the
 *      green phase transcribes them into toast.styles.ts.
 *   4. Stories carry the visual coverage (see toast.stories.tsx); this file is
 *      only for the edges a screenshot cannot make.
 *
 * ANATOMY, measured against @base-ui/react 1.6.0 in this browser (not guessed):
 *
 *   <Toast.Provider>                          <- renders NO element; owns the store
 *     <div tabindex="-1" role="region" aria-live="polite" aria-atomic="false"
 *          aria-relevant="additions text" aria-label="Notifications"
 *          style="--toast-frontmost-height:…">              <- Toast.Viewport
 *       <div role="dialog" tabindex="0" aria-modal="false" aria-labelledby
 *            aria-describedby style="--toast-index:… --toast-offset-y:… …">
 *                                                            <- Toast.Root
 *         <div>                                              <- Toast.Content
 *           <h2 id>                                          <- Toast.Title
 *           <p id>                                           <- Toast.Description
 *         <button type="button">                             <- Toast.Action
 *         <button type="button" aria-hidden="true">          <- Toast.Close
 *
 * Where Toast DIVERGES from every other Wave-2 overlay — read this before
 * copying a pattern from dialog.test.tsx:
 *
 *   - THE VIEWPORT IS NOT PORTALLED. `Toast.Portal` exists but is OPT-IN: with no
 *     portal around it the viewport stays exactly where it was rendered. The
 *     `part()`/`maybePart()` helpers still search `document.body` so the same
 *     helper block works either way, but the "everything is portalled" premise of
 *     the exemplar does not hold here.
 *   - THERE IS NO POINTER BLOCKER, because a toast is not modal (`aria-modal` is
 *     literally `false`). `userEvent.click()` from `vitest/browser` reaches the
 *     close and action buttons directly — measured — so this file uses real
 *     pointer input rather than the exemplar's direct `element.click()` escape.
 *   - THERE IS NO OPEN STATE AND NO TRIGGER. A toast is created imperatively
 *     through `useToastManager().add()` and identified by an id, so every fixture
 *     below passes an explicit `id` and every query is keyed to it.
 *   - REMOVAL IS DRIVEN BY TIMERS. Base UI's auto-dismiss is real `setTimeout`
 *     work, so the timeouts here are deliberately short constants and every
 *     absence assertion still goes through `await expect.poll(...)`, per the 2.4
 *     ruling — the unmount is animation-frame gated even where a measurement
 *     happened to be synchronous.
 *   - HOVERING THE VIEWPORT PAUSES EVERY TIMER (measured: a toast with an 80ms
 *     timeout survived 400ms under the pointer and vanished once it moved away).
 *     Since the mouse position persists between specs in a file, the one spec
 *     that hovers is LAST and moves the pointer off the viewport before it ends.
 *   - `data-starting-style` / `data-ending-style` only exist DURING a transition
 *     and are not asserted on an element anywhere; they are pinned as
 *     `data-[starting-style]:` / `data-[ending-style]:` modifiers inside the
 *     recipe class sets instead. So are `data-expanded`, `data-limited` and
 *     `data-type`, none of which a caller can pass as a prop.
 */

/**
 * How long the short-lived fixture toasts live, in milliseconds.
 *
 * Long enough that the auto-dismiss specs can still read the toast SYNCHRONOUSLY
 * after the real pointer click that adds it. The timer starts when the toast
 * mounts, so that first presence read — the positive control proving the toast
 * appeared at all — is racing this constant across a Playwright round trip. At
 * 80ms it lost that race under CPU contention ("expected null not to be null").
 * Every absence assertion polls with a 2000ms budget, so there is room here.
 */
const AUTO_DISMISS_MS = 500;

/** How long a "this must NOT happen" assertion waits before reading. */
const NON_EVENT_WAIT_MS = 250;

/**
 * How long the fixture's promise takes to settle, in milliseconds.
 *
 * Sized like {@link AUTO_DISMISS_MS} and for the same reason: the promise spec
 * reads `data-type === "loading"` SYNCHRONOUSLY after the pointer click that
 * starts it, so this constant has to outlast a Playwright round trip. At 40ms it
 * did not, and the toast had already re-typed to "success" by the first read.
 */
const PROMISE_SETTLE_MS = 500;

/**
 * Utilities `Toast.Viewport` must render. Base UI positions NOTHING for a
 * stacked toast (measured: the viewport's only inline style is the
 * `--toast-frontmost-height` custom property), so — exactly like `Dialog.Popup`
 * and unlike every anchored overlay — this recipe owns the placement outright.
 */
const VIEWPORT_CLASSES = [
  "fixed",
  "right-4",
  "bottom-4",
  "z-50",
  "flex",
  "w-80",
  "flex-col",
  "gap-2",
  "outline-none",
];

/**
 * Utilities `Toast.Root` must render. Three of the modifiers pin states no
 * caller can pass as a prop: `data-limited` is stamped on the toasts pushed past
 * the provider's `limit` (Base UI keeps them mounted so they can animate out
 * rather than vanishing), and `data-type` is stamped from the toast object,
 * which is how a promise toast reports failure.
 */
const ROOT_CLASSES = [
  "relative",
  "flex",
  "w-full",
  "flex-col",
  "gap-2",
  "rounded-lg",
  "border",
  "border-border",
  "bg-popover",
  "p-4",
  "text-popover-foreground",
  "shadow-lg",
  "outline-none",
  "transition-all",
  "duration-150",
  "data-[starting-style]:translate-y-2",
  "data-[starting-style]:opacity-0",
  "data-[ending-style]:translate-y-2",
  "data-[ending-style]:opacity-0",
  "data-[limited]:opacity-0",
  "data-[type=error]:border-destructive",
];

/** Utilities `Toast.Content` must render. */
const CONTENT_CLASSES = ["flex", "min-w-0", "flex-1", "flex-col", "gap-1"];

/** Utilities `Toast.Title` must render. */
const TITLE_CLASSES = ["text-sm", "font-semibold", "text-foreground"];

/** Utilities `Toast.Description` must render. */
const DESCRIPTION_CLASSES = ["text-sm", "text-muted-foreground"];

/** Utilities `Toast.Action` must render. */
const ACTION_CLASSES = [
  "inline-flex",
  "items-center",
  "justify-center",
  "rounded-md",
  "text-sm",
  "font-medium",
  "transition-colors",
  "hover:underline",
  "data-[disabled]:opacity-50",
];

/** Utilities `Toast.Close` must render. */
const CLOSE_CLASSES = [
  "inline-flex",
  "items-center",
  "justify-center",
  "rounded-md",
  "text-sm",
  "font-medium",
  "text-muted-foreground",
  "transition-colors",
  "hover:text-foreground",
  "data-[disabled]:opacity-50",
];

/**
 * Utilities `Toast.Positioner` must render. An ANCHORED toast is the one shape
 * where Base UI owns the placement itself — measured inline on the positioner:
 * `position:absolute`, `left`, `top`, a `transform` and the `--available-*` /
 * `--anchor-*` custom properties — so this recipe may only add stacking and
 * focus, exactly the rule `selectPositionerRecipe` established.
 */
const POSITIONER_CLASSES = ["z-50", "outline-none"];

/**
 * Utilities `Toast.Arrow` must render. Base UI places the arrow with an inline
 * `position:absolute` plus a side-dependent axis, so the recipe is size and
 * paint only.
 */
const ARROW_CLASSES = [
  "size-2",
  "rotate-45",
  "rounded-sm",
  "border",
  "border-border",
  "bg-popover",
];

/**
 * Every member `@base-ui/react/toast` publishes on its namespace, sorted. Toast
 * has no `Handle`/`createHandle` pair (unlike every other Wave-2 overlay) — its
 * imperative API is `useToastManager` for React callers and `createToastManager`
 * for everything else, and both are namespace members rather than parts.
 */
const BASE_UI_PART_NAMES = [
  "Action",
  "Arrow",
  "Close",
  "Content",
  "Description",
  "Portal",
  "Positioner",
  "Provider",
  "Root",
  "Title",
  "Viewport",
  "createToastManager",
  "useToastManager",
];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

/** The part carrying `data-testid`, searched across the whole document. */
function part(testId: string): HTMLElement {
  const element = document.body.querySelector<HTMLElement>(`[data-testid="${testId}"]`);
  expect(element, `no element with data-testid="${testId}"`).not.toBeNull();
  return element as HTMLElement;
}

/** The same lookup for parts that are legitimately absent. */
function maybePart(testId: string): HTMLElement | null {
  return document.body.querySelector<HTMLElement>(`[data-testid="${testId}"]`);
}

/** The ids of the toasts currently in the DOM, in DOM order. */
function toastIds(): string[] {
  return [
    ...document.body.querySelectorAll<HTMLElement>("[role='dialog'],[role='alertdialog']"),
  ].map((element) => element.dataset["testid"] ?? "");
}

/** A promise that settles the way the fixture's promise-toast specs need it to. */
function settle(succeed: boolean): Promise<string> {
  return new Promise<string>((resolve, reject) => {
    setTimeout(() => {
      if (succeed) {
        resolve("saved");
        return;
      }

      reject(new Error("network"));
    }, PROMISE_SETTLE_MS);
  });
}

/**
 * The imperative half of the fixture. Every toast is added with an explicit
 * `id`, so a spec can query `t-root-<id>` without knowing the generated one.
 */
function ToastControls(): ReactElement {
  const manager = useToastManager();
  const [settled, setSettled] = useState("pending");

  return (
    <div>
      <button
        type="button"
        data-testid="t-add"
        onClick={() => {
          manager.add({ id: "saved", title: "Saved", description: "Your changes are saved." });
        }}
      >
        Save
      </button>
      <button
        type="button"
        data-testid="t-add-queued"
        onClick={() => {
          manager.add({ id: "queued", title: "Queued", description: "Behind the first one." });
        }}
      >
        Queue
      </button>
      <button
        type="button"
        data-testid="t-add-brief"
        onClick={() => {
          manager.add({ id: "brief", title: "Brief", timeout: AUTO_DISMISS_MS });
        }}
      >
        Brief
      </button>
      <button
        type="button"
        data-testid="t-add-kept"
        onClick={() => {
          manager.add({ id: "kept", title: "Kept", timeout: 0 });
        }}
      >
        Keep
      </button>
      <button
        type="button"
        data-testid="t-add-undoable"
        onClick={() => {
          manager.add({ id: "undoable", title: "Deleted", actionProps: { children: "Undo" } });
        }}
      >
        Delete
      </button>
      <button
        type="button"
        data-testid="t-add-error"
        onClick={() => {
          manager.add({ id: "failed", title: "Failed", type: "error" });
        }}
      >
        Fail
      </button>
      <button
        type="button"
        data-testid="t-add-four"
        onClick={() => {
          for (const name of ["one", "two", "three", "four"]) {
            manager.add({ id: name, title: name });
          }
        }}
      >
        Four
      </button>
      <button
        type="button"
        data-testid="t-update"
        onClick={() => {
          manager.update("saved", { title: "Saved twice", description: "Updated in place." });
        }}
      >
        Update
      </button>
      <button
        type="button"
        data-testid="t-manager-close"
        onClick={() => {
          manager.close("saved");
        }}
      >
        Close saved
      </button>
      <button
        type="button"
        data-testid="t-manager-close-all"
        onClick={() => {
          manager.close();
        }}
      >
        Close all
      </button>
      <button
        type="button"
        data-testid="t-resolve"
        onClick={() => {
          manager
            .promise(settle(true), { loading: "Working…", success: "Done!", error: "Failed" })
            .then(
              (value) => setSettled(`resolved:${value}`),
              () => setSettled("rejected"),
            );
        }}
      >
        Resolve
      </button>
      <button
        type="button"
        data-testid="t-reject"
        onClick={() => {
          manager
            .promise(settle(false), { loading: "Working…", success: "Done!", error: "Failed" })
            .then(
              (value) => setSettled(`resolved:${value}`),
              () => setSettled("rejected"),
            );
        }}
      >
        Reject
      </button>
      <span data-testid="t-count">{manager.toasts.length}</span>
      <span data-testid="t-settled">{settled}</span>
    </div>
  );
}

/**
 * The rendered half of the fixture: every part at once, keyed off each toast's
 * id. `Toast.Title` and `Toast.Description` are deliberately childless — Base UI
 * fills them from the toast object, and that wiring is part of the contract.
 */
function ToastList(): ReactElement {
  const { toasts } = useToastManager();

  return (
    <>
      {toasts.map((toast) => (
        <Toast.Root key={toast.id} toast={toast} data-testid={`t-root-${toast.id}`}>
          <Toast.Content data-testid={`t-content-${toast.id}`}>
            <Toast.Title data-testid={`t-title-${toast.id}`} />
            <Toast.Description data-testid={`t-description-${toast.id}`} />
          </Toast.Content>
          <Toast.Action data-testid={`t-action-${toast.id}`} />
          <Toast.Close data-testid={`t-close-${toast.id}`}>Dismiss</Toast.Close>
        </Toast.Root>
      ))}
    </>
  );
}

interface ToastFixtureProps {
  /** The provider's default auto-dismiss, for the toasts that set none. */
  readonly timeout?: number;
  /** How many toasts stay unlimited before the oldest are marked `data-limited`. */
  readonly limit?: number;
}

/** The whole component under test: provider, controls and viewport. */
function ToastFixture({ timeout, limit }: ToastFixtureProps): ReactElement {
  return (
    <Toast.Provider timeout={timeout} limit={limit}>
      <ToastControls />
      <Toast.Viewport data-testid="t-viewport">
        <ToastList />
      </Toast.Viewport>
    </Toast.Provider>
  );
}

/** Renders the fixture and raises the default "Saved" toast through the manager. */
async function addSavedToast(props: ToastFixtureProps = {}): Promise<void> {
  await render(<ToastFixture {...props} />);

  await userEvent.click(part("t-add"));
  expect(maybePart("t-root-saved")).not.toBeNull();
}

describe("Toast", () => {
  it("exposes exactly Base UI's namespace members on one namespace object", () => {
    // The catalog-wide multi-part convention: keys mirror Base UI 1:1, so a
    // caller who knows the Base UI docs already knows this API.
    expect(Object.keys(Toast).toSorted()).toEqual(BASE_UI_PART_NAMES);
  });

  it("also exports both manager entry points at the top level", () => {
    // `Toast.useToastManager()` reads badly at a call site, so the hook and the
    // standalone factory are named exports too — the SAME functions, not copies.
    expect(useToastManager).toBe(Toast.useToastManager);
    expect(createToastManager).toBe(Toast.createToastManager);
  });

  it("renders the viewport as a politely announced, labelled region", async () => {
    await render(<ToastFixture />);

    const viewport = part("t-viewport");
    expect(viewport.getAttribute("role")).toBe("region");
    expect(viewport.getAttribute("aria-live")).toBe("polite");
    expect(viewport.getAttribute("aria-atomic")).toBe("false");
    expect(viewport.getAttribute("aria-label")).toBe("Notifications");
    expect(viewport.getAttribute("tabindex")).toBe("-1");
  });

  it("leaves the viewport where it was rendered rather than portalling it", async () => {
    // Toast is this wave's exception: `Toast.Portal` is opt-in, so an unportalled
    // viewport stays inside the caller's tree and no `data-base-ui-portal`
    // container is created at all.
    const { container } = await render(<ToastFixture />);

    expect(container.contains(part("t-viewport"))).toBe(true);
    expect(document.body.querySelector("[data-base-ui-portal]")).toBeNull();
  });

  it("renders no toast until the manager adds one", async () => {
    await render(<ToastFixture />);

    expect(toastIds()).toEqual([]);
    expect(part("t-count").textContent).toBe("0");
  });

  it("fills the title and description from the toast object", async () => {
    // The parts are rendered childless; Base UI supplies the text AND wires the
    // ids, and the wrappers must not disturb either.
    await addSavedToast();

    const root = part("t-root-saved");
    expect(root.getAttribute("role")).toBe("dialog");
    expect(root.getAttribute("aria-modal")).toBe("false");

    const title = part("t-title-saved");
    const description = part("t-description-saved");
    expect(title.tagName).toBe("H2");
    expect(title.textContent).toBe("Saved");
    expect(description.tagName).toBe("P");
    expect(description.textContent).toBe("Your changes are saved.");
    expect(root.getAttribute("aria-labelledby")).toBe(title.id);
    expect(root.getAttribute("aria-describedby")).toBe(description.id);
  });

  it("queues each new toast ahead of the ones already showing", async () => {
    await addSavedToast();

    await userEvent.click(part("t-add-queued"));

    // Newest first, measured: the most recent toast is the frontmost one.
    expect(toastIds()).toEqual(["t-root-queued", "t-root-saved"]);
    expect(part("t-count").textContent).toBe("2");
  });

  it("updates a toast in place when one is added with an id already showing", async () => {
    await addSavedToast();

    await userEvent.click(part("t-add"));

    expect(toastIds()).toEqual(["t-root-saved"]);
    expect(part("t-count").textContent).toBe("1");
  });

  it("rewrites a live toast through the manager's update", async () => {
    await addSavedToast();

    await userEvent.click(part("t-update"));

    expect(part("t-title-saved").textContent).toBe("Saved twice");
    expect(part("t-description-saved").textContent).toBe("Updated in place.");
    expect(part("t-count").textContent).toBe("1");
  });

  it("renders the viewport with its recipe", async () => {
    await render(<ToastFixture />);

    expect(classSet(part("t-viewport"))).toEqual(VIEWPORT_CLASSES.toSorted());
  });

  it("renders the root and content with their recipes", async () => {
    await addSavedToast();

    expect(classSet(part("t-root-saved"))).toEqual(ROOT_CLASSES.toSorted());
    expect(classSet(part("t-content-saved"))).toEqual(CONTENT_CLASSES.toSorted());
  });

  it("renders the title and description with their recipes", async () => {
    await addSavedToast();

    expect(classSet(part("t-title-saved"))).toEqual(TITLE_CLASSES.toSorted());
    expect(classSet(part("t-description-saved"))).toEqual(DESCRIPTION_CLASSES.toSorted());
  });

  it("renders the action and close buttons with their recipes", async () => {
    // `Toast.Action` renders NOTHING unless the toast supplies action content
    // (measured), so this fixture raises the one toast that carries `actionProps`.
    await render(<ToastFixture />);
    await userEvent.click(part("t-add-undoable"));

    const action = part("t-action-undoable");
    expect(action.tagName).toBe("BUTTON");
    expect(action.textContent).toBe("Undo");
    expect(classSet(action)).toEqual(ACTION_CLASSES.toSorted());
    expect(classSet(part("t-close-undoable"))).toEqual(CLOSE_CLASSES.toSorted());
  });

  it("renders no action button for a toast that supplies no action", async () => {
    await addSavedToast();

    expect(maybePart("t-action-saved")).toBeNull();
  });

  it("dismisses a toast when its close part is clicked", async () => {
    // Real pointer input, not the exemplar's direct `element.click()`: a toast is
    // not modal, so Base UI renders no pointer blocker over it.
    await addSavedToast();

    await userEvent.click(part("t-close-saved"));

    await expect.poll(() => maybePart("t-root-saved")).toBeNull();
    expect(part("t-count").textContent).toBe("0");
  });

  it("closes one toast by id and every toast when given none", async () => {
    await addSavedToast();
    await userEvent.click(part("t-add-queued"));

    await userEvent.click(part("t-manager-close"));

    await expect.poll(() => maybePart("t-root-saved")).toBeNull();
    expect(maybePart("t-root-queued")).not.toBeNull();

    await userEvent.click(part("t-manager-close-all"));

    await expect.poll(() => toastIds()).toEqual([]);
  });

  it("auto-dismisses a toast once its own timeout elapses", async () => {
    await render(<ToastFixture />);

    await userEvent.click(part("t-add-brief"));
    expect(maybePart("t-root-brief")).not.toBeNull();

    await expect.poll(() => maybePart("t-root-brief"), { timeout: 2000 }).toBeNull();
  });

  it("falls back to the provider's timeout for a toast that sets none", async () => {
    await addSavedToast({ timeout: AUTO_DISMISS_MS });

    await expect.poll(() => maybePart("t-root-saved"), { timeout: 2000 }).toBeNull();
  });

  it("never auto-dismisses a toast whose timeout is zero", async () => {
    // A NON-EVENT needs a real wait, not a poll: `expect.poll(...).not.toBeNull()`
    // resolves on its first read and would pass against a toast that was about to
    // vanish. The manual dismiss afterwards is the positive control — without it
    // this spec would also pass against a toast that was simply stuck.
    await render(<ToastFixture />);
    await userEvent.click(part("t-add-kept"));

    await new Promise((resolve) => {
      setTimeout(resolve, NON_EVENT_WAIT_MS);
    });

    expect(maybePart("t-root-kept")).not.toBeNull();

    await userEvent.click(part("t-close-kept"));

    await expect.poll(() => maybePart("t-root-kept")).toBeNull();
  });

  it("marks the toasts past the limit as limited instead of unmounting them", async () => {
    // Base UI's documented choice, confirmed by measurement: over-limit toasts
    // keep their element (so a fork can animate them out) and gain `data-limited`
    // plus `inert`. A recipe that hides them is the catalog's business, not
    // Base UI's — hence `data-[limited]:opacity-0` in ROOT_CLASSES.
    await render(<ToastFixture limit={2} />);

    await userEvent.click(part("t-add-four"));

    expect(toastIds()).toEqual(["t-root-four", "t-root-three", "t-root-two", "t-root-one"]);
    expect(part("t-root-four").hasAttribute("data-limited")).toBe(false);
    expect(part("t-root-three").hasAttribute("data-limited")).toBe(false);
    expect(part("t-root-two").hasAttribute("data-limited")).toBe(true);
    expect(part("t-root-one").hasAttribute("data-limited")).toBe(true);
    expect(part("t-root-one").hasAttribute("inert")).toBe(true);
  });

  it("limits to three toasts when the provider sets no limit", async () => {
    await render(<ToastFixture />);

    await userEvent.click(part("t-add-four"));

    expect(part("t-root-one").hasAttribute("data-limited")).toBe(true);
    expect(part("t-root-two").hasAttribute("data-limited")).toBe(false);
  });

  it("walks a promise toast from loading to success", async () => {
    // One toast, not two: `promise()` re-types the SAME element, which is why
    // `data-type` rather than a fresh toast is what a fork styles.
    await render(<ToastFixture />);

    await userEvent.click(part("t-resolve"));

    const loading = toastIds()[0] ?? "";
    expect(loading).not.toBe("");
    expect(part(loading).getAttribute("data-type")).toBe("loading");
    expect(part(loading).textContent).toContain("Working…");

    await expect
      .poll(() => part(loading).getAttribute("data-type"), { timeout: 2000 })
      .toBe("success");
    expect(part(loading).textContent).toContain("Done!");
    expect(toastIds()).toEqual([loading]);

    // The caller still gets the original promise's value back.
    await expect
      .poll(() => part("t-settled").textContent, { timeout: 2000 })
      .toBe("resolved:saved");
  });

  it("marks a rejected promise toast as an error and rethrows to the caller", async () => {
    await render(<ToastFixture />);

    await userEvent.click(part("t-reject"));

    const pending = toastIds()[0] ?? "";
    await expect
      .poll(() => part(pending).getAttribute("data-type"), { timeout: 2000 })
      .toBe("error");
    expect(part(pending).textContent).toContain("Failed");
    await expect.poll(() => part("t-settled").textContent, { timeout: 2000 }).toBe("rejected");
  });

  it("stamps the toast object's type onto the root", async () => {
    await render(<ToastFixture />);

    await userEvent.click(part("t-add-error"));

    expect(part("t-root-failed").getAttribute("data-type")).toBe("error");
  });

  it("drives the viewport from a manager created outside React", async () => {
    // The `createToastManager` half of the API: an app-wide singleton an API
    // client can raise a toast from, with no hook and no component in scope.
    const manager = createToastManager();

    await render(
      <Toast.Provider toastManager={manager}>
        <Toast.Viewport data-testid="t-viewport">
          <ToastList />
        </Toast.Viewport>
      </Toast.Provider>,
    );

    const id = manager.add({ id: "external", title: "External", description: "Raised outside." });
    expect(id).toBe("external");

    await expect.poll(() => maybePart("t-root-external")).not.toBeNull();
    expect(part("t-title-external").textContent).toBe("External");

    manager.close("external");

    await expect.poll(() => maybePart("t-root-external")).toBeNull();
  });

  it("leaves an anchored toast's placement to Base UI's inline styles", async () => {
    // The anchored shape is the mirror image of the stacked one: here Base UI
    // owns position/left/top/transform on the positioner and a side-dependent
    // axis on the arrow, so their recipes may only add stacking, focus, size and
    // paint. Copying the viewport's placement utilities onto them would fight it.
    await render(
      <Toast.Provider>
        <AnchoredFixture />
      </Toast.Provider>,
    );

    await userEvent.click(part("t-anchor"));

    const positioner = part("t-positioner");
    expect(positioner.style.position).toBe("absolute");
    expect(positioner.style.left).not.toBe("");
    expect(positioner.style.getPropertyValue("--available-width")).not.toBe("");
    expect(["top", "bottom", "left", "right"]).toContain(positioner.getAttribute("data-side"));
    expect(part("t-arrow").style.position).toBe("absolute");
    expect(part("t-root-anchored").style.position).toBe("");

    expect(classSet(positioner)).toEqual(POSITIONER_CLASSES.toSorted());
    expect(classSet(part("t-arrow"))).toEqual(ARROW_CLASSES.toSorted());
  });

  it("lets a caller className override root recipe utilities", async () => {
    // The cn()/tailwind-merge proof: the conflicting recipe utility is REMOVED
    // rather than appended-after, and untouched recipe utilities survive. A
    // string-append implementation leaves both backgrounds on and fails.
    await render(
      <Toast.Provider>
        <ToastControls />
        <Toast.Viewport data-testid="t-viewport">
          <OverriddenList />
        </Toast.Viewport>
      </Toast.Provider>,
    );

    await userEvent.click(part("t-add"));

    const root = part("t-root-saved");
    expect(root.classList.contains("bg-accent")).toBe(true);
    expect(root.classList.contains("bg-popover")).toBe(false);
    expect(root.classList.contains("w-96")).toBe(true);
    expect(root.classList.contains("w-full")).toBe(false);
    expect(root.classList.contains("border-border")).toBe(true);
    expect(root.classList.contains("data-[ending-style]:opacity-0")).toBe(true);
  });

  it("carries the root recipe onto another element through the render prop", async () => {
    // Base UI's `render` prop is much of the reason this catalog moved onto Base
    // UI at all: the recipe has to travel to whatever element the caller
    // substitutes.
    await render(
      <Toast.Provider>
        <ToastControls />
        <Toast.Viewport data-testid="t-viewport">
          <RenderPropList />
        </Toast.Viewport>
      </Toast.Provider>,
    );

    await userEvent.click(part("t-add"));

    const root = part("t-root-saved");
    expect(root.tagName).toBe("SECTION");
    expect(classSet(root)).toEqual(ROOT_CLASSES.toSorted());
  });

  it("passes through app-owned data-testid and native attributes", async () => {
    await render(
      <Toast.Provider>
        <ToastControls />
        <Toast.Viewport data-testid="t-viewport" aria-label="Alerts">
          <ToastList />
        </Toast.Viewport>
      </Toast.Provider>,
    );

    await userEvent.click(part("t-add"));

    expect(part("t-viewport").getAttribute("aria-label")).toBe("Alerts");
    expect(part("t-root-saved").dataset["testid"]).toBe("t-root-saved");
  });

  /*
   * LAST ON PURPOSE. Hovering the viewport pauses every auto-dismiss timer, and
   * the pointer position persists between specs in this file, so this one runs
   * after the timer specs and parks the pointer off the viewport before it ends.
   */
  it("expands the viewport while the pointer is over it", async () => {
    await addSavedToast();

    expect(part("t-viewport").hasAttribute("data-expanded")).toBe(false);
    expect(part("t-close-saved").getAttribute("aria-hidden")).toBe("true");

    await userEvent.hover(part("t-viewport"));

    expect(part("t-viewport").hasAttribute("data-expanded")).toBe(true);
    expect(part("t-root-saved").hasAttribute("data-expanded")).toBe(true);
    expect(part("t-close-saved").getAttribute("aria-hidden")).toBe("false");

    // Park the pointer somewhere harmless so the next file's timers still run.
    await userEvent.hover(part("t-add"));
    expect(part("t-viewport").hasAttribute("data-expanded")).toBe(false);
  });
});

/** The className-override fixture's list: one toast whose root fights the recipe. */
function OverriddenList(): ReactElement {
  const { toasts } = useToastManager();

  return (
    <>
      {toasts.map((toast) => (
        <Toast.Root
          key={toast.id}
          toast={toast}
          data-testid={`t-root-${toast.id}`}
          className="w-96 bg-accent"
        >
          <Toast.Title data-testid={`t-title-${toast.id}`} />
        </Toast.Root>
      ))}
    </>
  );
}

/**
 * The anchored fixture: a toast that points at a button instead of stacking in
 * the corner. The anchor is held in state rather than a ref so the positioner
 * re-renders once the button has mounted and has a box to measure.
 */
function AnchoredFixture(): ReactElement {
  const manager = useToastManager();
  const [anchor, setAnchor] = useState<HTMLButtonElement | null>(null);

  return (
    <>
      <button
        type="button"
        ref={setAnchor}
        data-testid="t-anchor"
        onClick={() => {
          manager.add({ id: "anchored", title: "Anchored" });
        }}
      >
        Anchor
      </button>
      <Toast.Viewport data-testid="t-viewport">
        {manager.toasts.map((toast) => (
          <Toast.Positioner key={toast.id} toast={toast} anchor={anchor} data-testid="t-positioner">
            <Toast.Root toast={toast} data-testid={`t-root-${toast.id}`}>
              <Toast.Title data-testid={`t-title-${toast.id}`} />
              <Toast.Arrow data-testid="t-arrow" />
            </Toast.Root>
          </Toast.Positioner>
        ))}
      </Toast.Viewport>
    </>
  );
}

/** The render-prop fixture's list: the same toast root rendered as a `<section>`. */
function RenderPropList(): ReactElement {
  const { toasts } = useToastManager();

  return (
    <>
      {toasts.map((toast) => (
        <Toast.Root
          key={toast.id}
          toast={toast}
          data-testid={`t-root-${toast.id}`}
          render={<section />}
        >
          <Toast.Title data-testid={`t-title-${toast.id}`} />
        </Toast.Root>
      ))}
    </>
  );
}
