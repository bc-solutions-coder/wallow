import { PageContainer } from "@bc-solutions-coder/ui";
import { createFileRoute } from "@tanstack/react-router";

import { notFoundOn404 } from "@shared/lib/not-found-on-404";

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
 * Both reads go through `notFoundOn404`, so a missing record is the
 * router's not-found (and a 404 response), not a 500 with the right screen.
 */
function InquiryDetailPage() {
  const { inquiryId } = Route.useParams();
  return (
    <PageContainer data-testid="dashboard-inquiry-detail">
      <InquiryDetail inquiryId={inquiryId} />
    </PageContainer>
  );
}

export const Route = createFileRoute("/dashboard/inquiries/$inquiryId")({
  loader: ({ context, params }) => {
    const path = { id: params.inquiryId };
    const inquiry = context.queryClient.ensureQueryData(
      inquiriesGetByIdOptions({ client: context.sdk.client, path }),
    );
    const comments = context.queryClient.ensureQueryData(
      inquiriesGetCommentsOptions({ client: context.sdk.client, path }),
    );
    // Both reads answer 404 for a missing inquiry, and whichever settles first
    // decides the loader's outcome — so the pair, not the by-id read alone,
    // is what becomes the router's not-found.
    return notFoundOn404(Promise.all([inquiry, comments]));
  },
  component: InquiryDetailPage,
});
