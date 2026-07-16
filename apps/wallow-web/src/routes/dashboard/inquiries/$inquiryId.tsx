import { createFileRoute } from "@tanstack/react-router";

import { inquiriesQueries } from "../../../features/inquiries/api";
import { InquiryDetail } from "../../../features/inquiries/components/InquiryDetail";

/**
 * The dashboard inquiry-detail route (Wallow-8w1h.7.4). Mirrors the canonical
 * organization-detail route (`$orgId.tsx`): the `loader` prefetches both the
 * inquiry detail and its comment thread via
 * `context.queryClient.ensureQueryData(...)`; `src/router.tsx` binds it under the
 * root via `.update({ id, path, getParentRoute })` (no dashboard layout route
 * yet). The page reads the `inquiryId` route param and renders `InquiryDetail`
 * (which owns all render coverage).
 */
function InquiryDetailPage() {
  const { inquiryId } = Route.useParams();
  return (
    <div data-testid="dashboard-inquiry-detail">
      <InquiryDetail inquiryId={inquiryId} />
    </div>
  );
}

export const Route = createFileRoute("/dashboard/inquiries/$inquiryId")({
  loader: ({ context, params }) =>
    Promise.all([
      context.queryClient.ensureQueryData(inquiriesQueries.detail(params.inquiryId)),
      context.queryClient.ensureQueryData(inquiriesQueries.comments(params.inquiryId)),
    ]),
  component: InquiryDetailPage,
});
