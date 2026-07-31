import { PageHeader } from "@bc-solutions-coder/ui";
import { createFileRoute } from "@tanstack/react-router";

import { CreateInquiryForm, inquiriesGetAllOptions, InquiryList } from "@features/inquiries";
import { PAGE_CONTAINER } from "@shared/lib/page-container";

/**
 * The dashboard inquiries index route (Wallow-8w1h.7.2) — copies the CANONICAL
 * organizations index route.
 *
 * The page root carries `data-testid="dashboard-inquiries"` and renders the
 * `InquiryList` component; the route `loader` prefetches the list via
 * `context.queryClient.ensureQueryData(inquiriesGetAllOptions({ client }))`,
 * binding the request-scoped client off the router context.
 *
 * Authored file-route style (`createFileRoute('/dashboard/inquiries/')`), so its
 * `id`/`path`/parent are left unset — `src/router.tsx` binds it under the root
 * via `.update({ id, path, getParentRoute })` (there is no dashboard layout route
 * yet; that lands in Phase 7).
 */
/**
 * The title block is the catalog `PageHeader` (Wallow-lrlm.5.1); the width is
 * the shared `PAGE_CONTAINER` rule. `space-y-8` stays on the root — that is this
 * page's vertical rhythm between the header, the list and the create card, not a
 * width, so the one-container rule leaves it alone.
 */
function InquiriesIndexPage() {
  return (
    <div data-testid="dashboard-inquiries" className={`${PAGE_CONTAINER} space-y-8`}>
      <PageHeader data-testid="inquiries-header" title="Inquiries" />
      <InquiryList />
      <CreateInquiryForm />
    </div>
  );
}

export const Route = createFileRoute("/dashboard/inquiries/")({
  loader: ({ context }) =>
    context.queryClient.ensureQueryData(inquiriesGetAllOptions({ client: context.sdk.client })),
  component: InquiriesIndexPage,
});
