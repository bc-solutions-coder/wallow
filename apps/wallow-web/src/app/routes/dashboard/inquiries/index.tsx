import { PageContainer, PageHeader } from "@bc-solutions-coder/ui";
import { createFileRoute } from "@tanstack/react-router";

import { CreateInquiryForm, inquiriesGetAllOptions, InquiryList } from "@features/inquiries";

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
 * The title block is the catalog `PageHeader`; the width is the catalog
 * `PageContainer`. `space-y-8` rides as a className override — that is this
 * page's vertical rhythm between the header, the list and the create card, not a
 * width, so `cn()` merges it alongside the shared column rather than over it.
 */
function InquiriesIndexPage() {
  return (
    <PageContainer data-testid="dashboard-inquiries" className="space-y-8">
      <PageHeader data-testid="inquiries-header" title="Inquiries" />
      <InquiryList />
      <CreateInquiryForm />
    </PageContainer>
  );
}

export const Route = createFileRoute("/dashboard/inquiries/")({
  loader: ({ context }) =>
    context.queryClient.ensureQueryData(inquiriesGetAllOptions({ client: context.sdk.client })),
  component: InquiriesIndexPage,
});
