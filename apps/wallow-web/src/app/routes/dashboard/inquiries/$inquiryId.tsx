import { createFileRoute } from "@tanstack/react-router";

import {
  InquiryDetail,
  inquiriesGetByIdOptions,
  inquiriesGetCommentsOptions,
} from "@features/inquiries";

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
    <div data-testid="dashboard-inquiry-detail" className="max-w-2xl mx-auto">
      <InquiryDetail inquiryId={inquiryId} />
    </div>
  );
}

export const Route = createFileRoute("/dashboard/inquiries/$inquiryId")({
  loader: ({ context, params }) =>
    Promise.all([
      context.queryClient.ensureQueryData(
        inquiriesGetByIdOptions({ client: context.sdk.client, path: { id: params.inquiryId } }),
      ),
      context.queryClient.ensureQueryData(
        inquiriesGetCommentsOptions({ client: context.sdk.client, path: { id: params.inquiryId } }),
      ),
    ]),
  component: InquiryDetailPage,
});
