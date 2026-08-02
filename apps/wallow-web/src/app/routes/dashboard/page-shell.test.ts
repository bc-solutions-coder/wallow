import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * The cross-page invariants no single page's spec can see: every dashboard route
 * page wraps its body in the catalog `PageContainer`, and every page title is
 * rendered by `PageHeader` rather than a hand-rolled `<h1>` or
 * `justify-between mb-8` row.
 *
 * Read from source, not the DOM, because the rule being pinned is "the width is
 * declared in one place" — a fact about the source.
 */

const dashboardDir: URL = new URL("./", import.meta.url);

function source(relativePath: string): string {
  return readFileSync(fileURLToPath(new URL(relativePath, dashboardDir)), "utf8");
}

/**
 * Every dashboard route page — the files that own a page ROOT element.
 * `route.tsx` is deliberately absent: it is the layout route, it renders
 * `DashboardLayout` and no page body of its own.
 */
const PAGE_FILES: readonly string[] = [
  "apps/index.tsx",
  "apps/register.tsx",
  "inquiries/$inquiryId.tsx",
  "inquiries/index.tsx",
  "my-organizations.tsx",
  "organizations/$orgId/index.tsx",
  "organizations/$orgId/members.tsx",
  "organizations/$orgId/requests.tsx",
  "organizations/index.tsx",
  "organizations/invitations.tsx",
  "settings.tsx",
];

/**
 * The pages that render a title of their own. The two detail pages are absent
 * because they delegate their whole body — heading included — to a feature
 * component (`OrganizationDetail`, `InquiryDetail`).
 */
const TITLED_PAGE_FILES: readonly string[] = [
  "apps/index.tsx",
  "apps/register.tsx",
  "inquiries/index.tsx",
  "my-organizations.tsx",
  "organizations/index.tsx",
  "organizations/invitations.tsx",
  "settings.tsx",
];

/** All dashboard route sources, including the layout route. */
const ROUTE_FILES: readonly string[] = [...PAGE_FILES, "route.tsx"];

const IMPORTS_PAGE_CONTAINER =
  /import\s*\{[^}]*\bPageContainer\b[^}]*\}\s*from\s*"@bc-solutions-coder\/ui"/u;

const IMPORTS_PAGE_HEADER =
  /import\s*\{[^}]*\bPageHeader\b[^}]*\}\s*from\s*"@bc-solutions-coder\/ui"/u;

/** A literal Tailwind max-width utility written into a source file. */
const LITERAL_MAX_WIDTH = /\bmax-w-[\w[\]./-]+/u;

/** The hand-rolled header row `PageHeader`'s own recipe makes unnecessary. */
const HAND_ROLLED_HEADER_ROW = /justify-between mb-8/u;

describe("dashboard pages share one container-width rule", () => {
  it.each(PAGE_FILES)("takes %s's width from the catalog container", (file) => {
    expect(source(file)).toMatch(IMPORTS_PAGE_CONTAINER);
  });

  it.each(PAGE_FILES)("leaves no hand-written width in %s", (file) => {
    // The width itself is `pageContainerRecipe`'s, pinned in packages/ui. What
    // this app owes is that no page decides one of its own.
    const found = source(file).match(LITERAL_MAX_WIDTH);

    expect(found?.[0] ?? null, "width belongs to PageContainer alone").toBeNull();
  });
});

describe("dashboard pages title themselves with PageHeader", () => {
  it.each(TITLED_PAGE_FILES)("composes the catalog header in %s", (file) => {
    expect(source(file)).toMatch(IMPORTS_PAGE_HEADER);
  });

  it.each(ROUTE_FILES)("hand-rolls no heading element in %s", (file) => {
    expect(source(file), "the page title is PageHeader's <h1>").not.toContain("<h1");
  });

  it.each(ROUTE_FILES)("hand-rolls no header row in %s", (file) => {
    expect(source(file), "the header row is PageHeader's own recipe").not.toMatch(
      HAND_ROLLED_HEADER_ROW,
    );
  });
});
