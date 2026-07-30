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
function InquiriesIndexPage() {
  return (
    <div data-testid="dashboard-inquiries" className="max-w-5xl mx-auto space-y-8">
      <h1 data-testid="inquiries-heading" className="text-3xl font-bold text-foreground">
        Inquiries
      </h1>
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
