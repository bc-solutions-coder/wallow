import { createFileRoute } from "@tanstack/react-router";

import { RegisterAppForm } from "../../../features/apps/components/RegisterAppForm";

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
 * `/dashboard` layout via `.update({ path, getParentRoute })`. The Blazor oracle
 * (`api/src/Wallow.Web/Components/Pages/Dashboard/RegisterApp.razor`) is
 * `@page "/dashboard/apps/register"`, confirming the path.
 */
function RegisterAppPage() {
  return (
    <div data-testid="dashboard-apps-register">
      <RegisterAppForm />
    </div>
  );
}

export const Route = createFileRoute("/dashboard/apps/register")({
  component: RegisterAppPage,
});
