import { createFileRoute } from "@tanstack/react-router";

import { inquiriesQueries } from "../../../features/inquiries/api";
import { InquiryList } from "../../../features/inquiries/components/InquiryList";

/**
 * The dashboard inquiries index route (Wallow-8w1h.7.2) — copies the CANONICAL
 * organizations index route.
 *
 * The page root carries `data-testid="dashboard-inquiries"` and renders the
 * `InquiryList` component; the route `loader` prefetches the list via
 * `context.queryClient.ensureQueryData(inquiriesQueries.list())`.
 *
 * Authored file-route style (`createFileRoute('/dashboard/inquiries/')`), so its
 * `id`/`path`/parent are left unset — `src/router.tsx` binds it under the root
 * via `.update({ id, path, getParentRoute })` (there is no dashboard layout route
 * yet; that lands in Phase 7).
 */
function InquiriesIndexPage() {
  return (
    <div data-testid="dashboard-inquiries">
      <InquiryList />
    </div>
  );
}

export const Route = createFileRoute("/dashboard/inquiries/")({
  loader: ({ context }) => context.queryClient.ensureQueryData(inquiriesQueries.list()),
  component: InquiriesIndexPage,
});
