import type { ReactElement, ReactNode } from "react";

import { toAppHref } from "@shared/lib/base-path";

/**
 * The two ToS/Privacy consent labels, shared by the register form and the
 * external-login consent gate — the two screens that ask for the same agreement.
 *
 * They live here rather than in either feature because `wallow/zone-dag` forbids
 * a feature importing a feature, and because the accessible NAME of a consent
 * box is a legal artifact: the two screens must ask for consent in identical
 * words or the record of what was agreed to differs by route.
 */

/**
 * A consent box's label: prose with the policy link inside it.
 *
 * Inside rather than beside, because the label IS the checkbox's accessible
 * name — "I agree to the Terms of Service" — and an anchor rendered as a sibling
 * would leave the box named by half its own sentence.
 *
 * `target="_blank"`: reading the document must not abandon the flow. On the
 * consent gate that would cost the user a 10-minute `ExternalLoginState` cookie
 * they cannot re-obtain without signing in with the provider again; on the
 * register form it would cost them everything they had typed.
 */
function ConsentLabel({
  href,
  text,
}: {
  readonly href: string;
  readonly text: string;
}): ReactElement {
  return (
    <>
      I agree to the{" "}
      <a
        href={href}
        target="_blank"
        rel="noopener noreferrer"
        className="text-primary underline-offset-4 hover:underline"
      >
        {text}
      </a>
    </>
  );
}

/*
 * Built once at module scope, NOT inline in `label={...}`: `react/jsx-max-depth`
 * counts an attribute's JSX as one level deeper than the element carrying it, so
 * an inline `ConsentLabel` under `AppField > CheckboxField` is depth 3 against a
 * budget of 2.
 */

export const TERMS_CONSENT_LABEL: ReactNode = (
  <ConsentLabel href={toAppHref("/terms")} text="Terms of Service" />
);

export const PRIVACY_CONSENT_LABEL: ReactNode = (
  <ConsentLabel href={toAppHref("/privacy")} text="Privacy Policy" />
);
