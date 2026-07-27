import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it, vi } from "vitest";

import { Avatar, type AvatarFallbackProps, type AvatarImageProps } from "./avatar";

/*
 * Avatar behavioural spec (Wallow-m5aq.4.3), shaped after the Wallow-m5aq.2.1
 * Button exemplar:
 *
 *   1. Runs in the vitest BROWSER project — real headless Chromium, real Base UI,
 *      real DOM. Nothing is mocked, and in particular nothing here is a fake
 *      image: every `src` below is a `data:` URI, so the load and the failure
 *      are both the browser's own and no network is touched.
 *   2. The recipes are asserted THROUGH the component, never by importing
 *      `avatarRootRecipe` and inspecting its return value: a recipe unit test
 *      would pass while the component forgot to apply it.
 *   3. Class assertions are an ORDER-FREE SET (`classSet`), because
 *      `cn()`/tailwind-merge is free to reorder.
 *   4. Stories carry the visual coverage (see avatar.stories.tsx); this file is
 *      only for the edges a screenshot cannot make.
 *
 * ANATOMY, measured against @base-ui/react 1.6.0 in this browser (not guessed —
 * every line below came off a throwaway probe run before this spec was written):
 *
 *   <span>                          <- Avatar.Root
 *     <img alt src>                 <- Avatar.Image,    ONLY once decoded
 *     <span>AB</span>               <- Avatar.Fallback, ONLY while there is no
 *                                      decoded image
 *
 * Four measurements are worth stating because they are easy to assume wrong:
 *
 *   - NO PART CARRIES A STATE `data-*` ATTRIBUTE. There is no
 *     `data-loading-status` on the root and no `data-error` anywhere; the avatar
 *     publishes its status ONLY through React state and the image's
 *     `onLoadingStatusChange` callback. The parts mount and unmount instead,
 *     which is why every state assertion below is a presence assertion and why
 *     the recipes carry no `data-[...]:` modifiers.
 *   - `Avatar.Image` IS ABSENT FROM THE DOM WHILE LOADING — not hidden, not
 *     zero-opacity. It appears only on success, and on failure it never appears
 *     at all.
 *   - `onLoadingStatusChange` reports the sequence `loading` -> `loaded` or
 *     `loading` -> `error`. `idle` is in the type but was never observed for an
 *     image that has a `src`.
 *   - An `<img>` freshly mounted by Base UI carries a transient
 *     `data-starting-style=""` (its enter transition) that clears on a later
 *     frame. Nothing below asserts the image's attribute set for that reason.
 *
 * TIMING. Both transitions are asynchronous, so every assertion about them polls:
 * `vi.waitFor` to wait for a part to APPEAR, `expect.poll` to prove one stays
 * ABSENT. A synchronous read straight after `render()` sees the pre-load DOM.
 *
 * IMAGE CACHE — measured, and the reason for the `#seed` on every `src` below.
 * Chromium caches a decoded `data:` URI for the whole file, and a cached image
 * decodes SYNCHRONOUSLY: Base UI then reports `['loaded']` alone and never
 * reports `loading` at all. A spec reusing another spec's `src` therefore passes
 * or fails depending on which ran first. A unique URL fragment is enough to miss
 * the cache, so every spec here mints its own `src` and none depends on file
 * order.
 */

/** A real 1x1 PNG the browser can actually decode. */
const PNG_PIXEL =
  "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

/** Well-formed base64 that is not a PNG: the decode fails, so the load errors. */
const BROKEN_PNG = "data:image/png;base64,VGhpc0lzTm90QVBORw==";

/** A decodable photo no other spec has loaded — see IMAGE CACHE in the header. */
function photo(seed: string): string {
  return `${PNG_PIXEL}#${seed}`;
}

/** An undecodable photo no other spec has loaded. */
function brokenPhoto(seed: string): string {
  return `${BROKEN_PNG}#${seed}`;
}

/** Utilities `Avatar.Root` must render. */
const ROOT_CLASSES = [
  "relative",
  "inline-flex",
  "size-10",
  "shrink-0",
  "items-center",
  "justify-center",
  "overflow-hidden",
  "rounded-full",
  "bg-muted",
  "select-none",
];

/** Utilities `Avatar.Image` must render. */
const IMAGE_CLASSES = ["size-full", "object-cover"];

/** Utilities `Avatar.Fallback` must render. */
const FALLBACK_CLASSES = [
  "flex",
  "size-full",
  "items-center",
  "justify-center",
  "text-sm",
  "font-medium",
  "text-muted-foreground",
];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

/** The element carrying `data-testid`, failing loudly rather than returning null. */
function part(container: HTMLElement, testId: string): HTMLElement {
  const element = container.querySelector(`[data-testid="${testId}"]`);
  expect(element, testId).not.toBeNull();
  return element as HTMLElement;
}

/** The element carrying `data-testid`, or null — for absence assertions. */
function maybePart(container: HTMLElement, testId: string): Element | null {
  return container.querySelector(`[data-testid="${testId}"]`);
}

/** A complete avatar: a photo with initials behind it, the only realistic shape. */
function UserAvatar({
  src,
  imageProps,
  fallbackProps,
}: {
  readonly src?: string;
  readonly imageProps?: Partial<AvatarImageProps>;
  readonly fallbackProps?: Partial<AvatarFallbackProps>;
}) {
  return (
    <Avatar.Root data-testid="avatar">
      <Avatar.Image src={src} alt="Ada Lovelace" data-testid="avatar-image" {...imageProps} />
      <Avatar.Fallback data-testid="avatar-fallback" {...fallbackProps}>
        AL
      </Avatar.Fallback>
    </Avatar.Root>
  );
}

describe("Avatar", () => {
  it("renders the root recipe on Base UI's frame element", async () => {
    const { container } = await render(<UserAvatar />);

    const root = part(container, "avatar");
    expect(root.tagName).toBe("SPAN");
    expect(classSet(root)).toEqual(ROOT_CLASSES.toSorted());
  });

  it("shows the fallback, carrying its recipe, when no image is provided", async () => {
    const { container } = await render(<UserAvatar />);

    const fallback = part(container, "avatar-fallback");
    expect(fallback.tagName).toBe("SPAN");
    expect(fallback.textContent).toBe("AL");
    expect(classSet(fallback)).toEqual(FALLBACK_CLASSES.toSorted());
  });

  it("never mounts an image element when there is no src", async () => {
    const { container } = await render(<UserAvatar />);

    await expect.poll(() => maybePart(container, "avatar-image")).toBeNull();
  });

  it("swaps the fallback for the image, carrying its recipe, once the photo decodes", async () => {
    const { container } = await render(<UserAvatar src={photo("decoded")} />);

    await vi.waitFor(() => {
      expect(maybePart(container, "avatar-image")).not.toBeNull();
    });

    const image = part(container, "avatar-image");
    expect(image.tagName).toBe("IMG");
    expect(image.getAttribute("alt")).toBe("Ada Lovelace");
    expect(classSet(image)).toEqual(IMAGE_CLASSES.toSorted());
    expect(maybePart(container, "avatar-fallback")).toBeNull();
  });

  it("reports the loading sequence for a photo that decodes", async () => {
    const onLoadingStatusChange = vi.fn();
    await render(<UserAvatar src={photo("sequence")} imageProps={{ onLoadingStatusChange }} />);

    await vi.waitFor(() => {
      expect(onLoadingStatusChange).toHaveBeenCalledWith("loaded");
    });
    expect(onLoadingStatusChange.mock.calls.at(0)?.[0]).toBe("loading");
  });

  it("falls back to the initials when the photo fails to load", async () => {
    // The acceptance criterion of this task, and the only behaviour a caller
    // cannot implement itself: no branch, no error handler, no `onError` — Base
    // UI keeps the fallback mounted and never mounts the <img> at all. The
    // status callback is what makes the failure OBSERVABLE, so it is the poll
    // target; the DOM assertions are taken after it has fired.
    const onLoadingStatusChange = vi.fn();
    const { container } = await render(
      <UserAvatar src={brokenPhoto("failed")} imageProps={{ onLoadingStatusChange }} />,
    );

    await vi.waitFor(() => {
      expect(onLoadingStatusChange).toHaveBeenCalledWith("error");
    });

    const fallback = part(container, "avatar-fallback");
    expect(fallback.textContent).toBe("AL");
    expect(classSet(fallback)).toEqual(FALLBACK_CLASSES.toSorted());
    await expect.poll(() => maybePart(container, "avatar-image")).toBeNull();
  });

  it("brings the fallback back when a decoded photo is replaced by a broken one", async () => {
    // The recovery half: an avatar whose `src` changes under it must not be left
    // showing a stale photo or an empty circle.
    const onLoadingStatusChange = vi.fn();
    const result = await render(<UserAvatar src={photo("replaced")} />);

    await vi.waitFor(() => {
      expect(maybePart(result.container, "avatar-image")).not.toBeNull();
    });

    await result.rerender(
      <UserAvatar src={brokenPhoto("replacement")} imageProps={{ onLoadingStatusChange }} />,
    );

    await vi.waitFor(() => {
      expect(onLoadingStatusChange).toHaveBeenCalledWith("error");
    });
    expect(part(result.container, "avatar-fallback").textContent).toBe("AL");
    await expect.poll(() => maybePart(result.container, "avatar-image")).toBeNull();
  });

  it("holds the fallback back for its delay so a fast photo cannot flash initials", async () => {
    const { container } = await render(<UserAvatar fallbackProps={{ delay: 150 }} />);

    // Synchronous on purpose: the point is that the frame is EMPTY on the first
    // frame, which a poll would paper over.
    expect(maybePart(container, "avatar-fallback")).toBeNull();

    await vi.waitFor(
      () => {
        expect(maybePart(container, "avatar-fallback")).not.toBeNull();
      },
      { timeout: 2000 },
    );
    expect(classSet(part(container, "avatar-fallback"))).toEqual(FALLBACK_CLASSES.toSorted());
  });

  it("lets a caller className override a recipe utility on the root", async () => {
    // The cn()/tailwind-merge proof: the conflicting recipe utility is REMOVED
    // rather than appended-after, and utilities the caller never mentioned
    // survive. A string-append implementation leaves both `size-10` and
    // `size-16` on the element.
    const { container } = await render(
      <Avatar.Root className="size-16" data-testid="avatar">
        <Avatar.Fallback data-testid="avatar-fallback">AL</Avatar.Fallback>
      </Avatar.Root>,
    );

    const root = part(container, "avatar");
    expect(root.classList.contains("size-16")).toBe(true);
    expect(root.classList.contains("size-10")).toBe(false);
    expect(root.classList.contains("rounded-full")).toBe(true);
    expect(root.classList.contains("overflow-hidden")).toBe(true);
    expect(root.classList.contains("bg-muted")).toBe(true);
  });

  it("lets a caller className override a recipe utility on the fallback", async () => {
    const { container } = await render(
      <Avatar.Root data-testid="avatar">
        <Avatar.Fallback className="bg-primary" data-testid="avatar-fallback">
          AL
        </Avatar.Fallback>
      </Avatar.Root>,
    );

    const fallback = part(container, "avatar-fallback");
    expect(fallback.classList.contains("bg-primary")).toBe(true);
    expect(fallback.classList.contains("size-full")).toBe(true);
    expect(fallback.classList.contains("text-muted-foreground")).toBe(true);
  });

  it("composes the root recipe onto another element through the render prop", async () => {
    const { container } = await render(
      <Avatar.Root render={<div />} data-testid="avatar">
        <Avatar.Fallback data-testid="avatar-fallback">AL</Avatar.Fallback>
      </Avatar.Root>,
    );

    const root = part(container, "avatar");
    expect(root.tagName).toBe("DIV");
    expect(classSet(root)).toEqual(ROOT_CLASSES.toSorted());
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(
      <Avatar.Root data-testid="member-avatar">
        <Avatar.Fallback data-testid="member-avatar-initials">AL</Avatar.Fallback>
      </Avatar.Root>,
    );

    expect(container.querySelector('[data-testid="member-avatar"]')).not.toBeNull();
    expect(container.querySelector('[data-testid="member-avatar-initials"]')).not.toBeNull();
  });
});
