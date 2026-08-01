import { PageContainer, PageHeader } from "@bc-solutions-coder/ui";
import { createFileRoute } from "@tanstack/react-router";

import { RegisterAppForm } from "@features/apps";

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
 * The title block is the catalog `PageHeader` and the width is the catalog
 * `PageContainer`, so a write-only page sits in the same column as the list
 * pages. The `mb-8` rhythm under the heading is the header row's own.
 */
function RegisterAppPage() {
  return (
    <PageContainer data-testid="dashboard-apps-register">
      <PageHeader data-testid="apps-register-header" title="Register New App" />
      <RegisterAppForm />
    </PageContainer>
  );
}

export const Route = createFileRoute("/dashboard/apps/register")({
  component: RegisterAppPage,
});
