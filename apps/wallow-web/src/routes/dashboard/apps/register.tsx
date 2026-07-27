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
 * `/dashboard` layout via `.update({ path, getParentRoute })`. The route path is
 * `/dashboard/apps/register`.
 */
function RegisterAppPage() {
  return (
    <div data-testid="dashboard-apps-register" className="max-w-2xl mx-auto">
      <h1 data-testid="apps-register-heading" className="text-3xl font-bold text-foreground mb-8">
        Register New App
      </h1>
      <RegisterAppForm />
    </div>
  );
}

export const Route = createFileRoute("/dashboard/apps/register")({
  component: RegisterAppPage,
});
