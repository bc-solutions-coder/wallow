import { PageHeader } from "@bc-solutions-coder/ui";
import { createFileRoute } from "@tanstack/react-router";

import { RegisterAppForm } from "@features/apps";
import { PAGE_CONTAINER } from "@shared/lib/page-container";

/**
 * The dashboard register-app route (Wallow-ffpq.3.5) — the intended mount point
 * for the orphan `RegisterAppForm`
 * (`features/apps/components/RegisterAppForm.tsx`), which is fully implemented
 * and unit-tested but had ZERO non-test importers before this route existed.
 *
 * The page root carries `data-testid="dashboard-apps-register"` and renders the
 * `RegisterAppForm` component. Unlike the list routes there is no `loader` — the
 * form is write-only (it fires `apps.register` on submit and reveals the one-time
 * client secret from the mutation result), so nothing needs prefetching.
 *
 * Authored file-route style (`createFileRoute('/dashboard/apps/register')`), so
 * its `id`/`path`/parent are left unset — `src/router.tsx` binds it under the
 * `/dashboard` layout via `.update({ path, getParentRoute })`. The route path is
 * `/dashboard/apps/register`.
 */
/**
 * The title block is the catalog `PageHeader` (Wallow-lrlm.5.1) and the width is
 * the shared `PAGE_CONTAINER` rule — this page used to run narrower than the
 * list pages; F5.T1 collapses that split onto one container. The `mb-8` the
 * hand-rolled heading carried is the header row's now.
 */
function RegisterAppPage() {
  return (
    <div data-testid="dashboard-apps-register" className={PAGE_CONTAINER}>
      <PageHeader data-testid="apps-register-header" title="Register New App" />
      <RegisterAppForm />
    </div>
  );
}

export const Route = createFileRoute("/dashboard/apps/register")({
  component: RegisterAppPage,
});
