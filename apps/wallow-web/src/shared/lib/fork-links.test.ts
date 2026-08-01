import { FORK_LINKS_GLOBAL_KEY, forkLinks as buildTimeForkLinks } from "@bc-solutions-coder/styles";
import { afterEach, describe, expect, it } from "vitest";

import { forkLinks } from "./fork-links";

/**
 * The accessor's precedence. There is no request in scope here, so this covers
 * the two ends of the chain: what the server published into the document, and
 * the fork's own pair for a caller with neither a document nor a request —
 * which is what lets a screen mount in a spec with no provider and no router.
 */
describe("forkLinks", () => {
  afterEach(() => {
    delete (globalThis as Record<string, unknown>)[FORK_LINKS_GLOBAL_KEY];
  });

  it("answers with the links the document published", () => {
    const deployed = {
      repositoryUrl: "https://git.example.test/acme/app",
      docsUrl: "https://docs.example.test/",
    };
    (globalThis as Record<string, unknown>)[FORK_LINKS_GLOBAL_KEY] = deployed;

    expect(forkLinks()).toEqual(deployed);
  });

  it("falls back to the fork's build-time pair when nothing published any", () => {
    expect(forkLinks()).toEqual(buildTimeForkLinks);
  });

  it("ignores a global that is not a pair of links", () => {
    (globalThis as Record<string, unknown>)[FORK_LINKS_GLOBAL_KEY] = { repositoryUrl: 7 };

    expect(forkLinks()).toEqual(buildTimeForkLinks);
  });
});
