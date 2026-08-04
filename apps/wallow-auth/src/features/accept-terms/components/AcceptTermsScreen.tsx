import { AppForm, SubmitButton, useAppForm } from "@bc-solutions-coder/forms";
import { MutedText, Text } from "@bc-solutions-coder/ui";
import type { ReactElement, ReactNode } from "react";
import { z } from "zod";

import { AuthScreen } from "@shared/components/auth-screen";
import { PRIVACY_CONSENT_LABEL, TERMS_CONSENT_LABEL } from "@shared/components/consent-labels";
import { toAppHref } from "@shared/lib/base-path";
import { bounceBackMessage, completeRegistrationUrl } from "../accept-terms-handoff";

/**
 * The AcceptTerms screen (Wallow-vec7.3.10).
 *
 * This is the ToS/Privacy GATE in the external-login (social sign-up) flow — not
 * the static terms document, which is the separate `/terms` route. Testids come
 * verbatim from the oracle: `accept-terms-heading`, `accept-terms-error`,
 * `accept-terms-checkbox`, `accept-terms-privacy-checkbox`,
 * `accept-terms-submit`. The two boxes pin theirs because their field names
 * would derive `-terms-accepted` / `-privacy-accepted`.
 *
 * Four of the props are the oracle's `[SupplyParameterFromQuery]` properties;
 * the fifth, `clientId`, is the relay Wallow-53kr added. The route owns the
 * query string and hands them down, keeping this component a pure function of
 * its inputs and testable without a router.
 *
 * The endpoint hand-off, its two injection guards, and the bounce-back
 * code→copy mapping live in `../accept-terms-handoff`, which also carries the
 * reasoning for the two decisions this screen is most likely to be "fixed" on:
 * why the hand-off is same-origin, and why `isSafeReturnUrl` is deliberately not
 * applied.
 *
 * ── WHY THIS FORM RUNS THE FORMS PACKAGE "SIDEWAYS" ──────────────────────────
 *
 * There is no request to make: the user's identity lives entirely in an HttpOnly
 * cookie this screen cannot read, and consent finishes by handing the browser to
 * an endpoint the cookie rides along with. So it takes the plain-`onSubmit`
 * escape hatch, and all `useAppForm` owns here is the two booleans and their
 * boxes.
 */

/** What the gate's form holds. */
interface ConsentValues {
  readonly termsAccepted: boolean;
  readonly privacyAccepted: boolean;
}

/**
 * RULE-FREE on purpose. `revalidateLogic` runs this on submit and aborts
 * `handleSubmit` on any failure — and this screen has no field messages and no
 * banner of its own to report that in, because consent is gated by a DISABLED
 * submit rather than by a rejection. It is here for the value types alone.
 */
const consentSchema = z.object({
  termsAccepted: z.boolean(),
  privacyAccepted: z.boolean(),
});

const NOTHING_ACCEPTED: ConsentValues = { termsAccepted: false, privacyAccepted: false };

/** The oracle's `BbCardHeader`. */
const TITLE = "Almost there!";
const DESCRIPTION = "Please accept our terms to create your account";

/**
 * The oracle's `Disabled="@(!_termsAccepted || !_privacyAccepted)"`. Derived on
 * every render, never latched forward: consent is revocable right up to the
 * click.
 */
function bothAccepted(values: ConsentValues): boolean {
  return values.termsAccepted && values.privacyAccepted;
}

/**
 * The oracle's `@if (!string.IsNullOrEmpty(Email))` block — the user's only
 * chance to notice the provider handed over the wrong account BEFORE one gets
 * created. Gated on the email, so a link carrying no address renders nothing
 * here rather than an empty identity card.
 */
function SigningUpAs({ email, name }: { readonly email: string; readonly name?: string }) {
  return (
    <div className="rounded-md border border-border p-3 text-sm">
      <MutedText>Signing up as</MutedText>
      {name === undefined || name === "" ? null : (
        <Text as="p" variant="bodySm" weight="medium">
          {name}
        </Text>
      )}
      <MutedText>{email}</MutedText>
    </div>
  );
}

/**
 * The oracle's card footer, and the only "decline" affordance the screen has.
 * Walking away creates no account and leaves the ExternalLoginState cookie to
 * expire on its own; there is nothing to clean up client-side.
 *
 * NOT `QuietLink`: that recipe is the muted standalone secondary link, and this
 * is an accent-coloured call to action sitting inside muted prose — the same
 * distinction `RegisterForm`'s footer keeps.
 */
function ChangedYourMind(): ReactElement {
  return (
    <MutedText className="text-center">
      Changed your mind?{" "}
      <a href={toAppHref("/login")} className="text-primary underline-offset-4 hover:underline">
        Back to sign in
      </a>
    </MutedText>
  );
}

export interface AcceptTermsScreenProps {
  /** The `returnUrl` query parameter — `undefined` when the link omits it. */
  readonly returnUrl?: string;
  /**
   * The `client_id` query parameter — the client that started the external-login
   * flow, `undefined` when the flow carries none (Wallow-53kr). The screen is a
   * relay for this value and nothing more; what it owes the value is the
   * encoding `completeRegistrationUrl` applies.
   */
  readonly clientId?: string;
  /** The `email` query parameter — the address the external provider vouched for. */
  readonly email?: string;
  /** The `name` query parameter — the provider's display name for the user. */
  readonly name?: string;
  /** The `error` query parameter — a bounce-back reason code. */
  readonly error?: string;
}

/**
 * The consent form itself, split out of the screen so `AuthScreen` is not one of
 * the levels its fields are counted under: `react/jsx-max-depth` is 2 and
 * `pnpm lint` runs `--deny-warnings`. This is `RegisterFields`' split, for
 * `RegisterFields`' reason.
 */
function ConsentGate({
  returnUrl,
  clientId,
}: {
  readonly returnUrl: string | undefined;
  readonly clientId: string | undefined;
}): ReactElement {
  const form = useAppForm({
    schema: consentSchema,
    defaultValues: NOTHING_ACCEPTED,
    onSubmit: (values: ConsentValues): void => {
      // The oracle re-checks inside its handler rather than trusting the
      // disabled attribute, and so does this: declining is simply not accepting,
      // and a forced click must be inert rather than merely unclickable. The
      // screen never sends `acceptedTerms=false` — the endpoint has that branch,
      // but it is not ours to drive; there is no "no thanks" round trip.
      if (!bothAccepted(values)) {
        return;
      }

      // A FULL navigation — the oracle's `NavigateTo(url, forceLoad: true)`,
      // never `router.navigate`: `/v1/**` is served by the passthrough reverse
      // proxy, not by the client-side route tree, which would 404 in-app. It
      // must also be a real top-level navigation for the browser to attach the
      // SameSite=Lax ExternalLoginState cookie the endpoint needs (bd memory
      // `full-navigation-seam-for-wallow-auth-screens-that`).
      globalThis.location.href = completeRegistrationUrl(returnUrl, clientId);
    },
  });

  return (
    <AppForm form={form} testIdPrefix="accept-terms" className="space-y-4">
      <form.AppField name="termsAccepted">
        {(field) => (
          <field.CheckboxField label={TERMS_CONSENT_LABEL} testId="accept-terms-checkbox" />
        )}
      </form.AppField>

      <form.AppField name="privacyAccepted">
        {(field) => (
          <field.CheckboxField
            label={PRIVACY_CONSENT_LABEL}
            testId="accept-terms-privacy-checkbox"
          />
        )}
      </form.AppField>

      <form.Subscribe<ConsentValues> selector={(state) => state.values}>
        {(values: ConsentValues) => (
          <SubmitButton disabled={!bothAccepted(values)}>Create Account</SubmitButton>
        )}
      </form.Subscribe>
    </AppForm>
  );
}

export function AcceptTermsScreen({
  returnUrl,
  clientId,
  email,
  name,
  error,
}: AcceptTermsScreenProps): ReactNode {
  return (
    <AuthScreen
      title={TITLE}
      description={DESCRIPTION}
      headingTestId="accept-terms-heading"
      error={error === undefined || error === "" ? null : bounceBackMessage(error)}
      errorTestId="accept-terms-error"
      footer={<ChangedYourMind />}
    >
      {email === undefined || email === "" ? null : <SigningUpAs email={email} name={name} />}
      <ConsentGate returnUrl={returnUrl} clientId={clientId} />
    </AuthScreen>
  );
}
