import { Button, Field, Input, Label, Text } from "@bc-solutions-coder/ui";
import { useMutation, useQueryClient } from "@bc-solutions-coder/query";
import { useRouteContext } from "@tanstack/react-router";
import { type ReactNode, useEffect, useRef, useState } from "react";
import { accountSendMagicLinkMutation, accountVerifyMagicLinkOptions } from "../api";
import { GENERIC_MESSAGE } from "../auth-result";
import {
  BLANK_EMAIL_MESSAGE,
  MAGIC_LINK_SENT_MESSAGE,
  magicLinkWasSent,
  sendMagicLinkFailureMessage,
  verifyMagicLinkFailureMessage,
} from "../magic-link-result";
import type { LoginPanelProps } from "../panel";

/**
 * The MAGIC-LINK tab of the login screen (Wallow-vec7.3.12 / 2.8b), ported from the
 * oracle's `_activeTab == LoginTab.MagicLink` branch, its `HandleSendMagicLink` and
 * its `HandleVerifyMagicLink` (`api/src/Wallow.Auth/Components/Pages/Login.razor`
 * :105-137, :368-400, :402-430).
 *
 * Per the contract Wallow-vec7.3.11 left on the bead, this panel owns ONLY what the
 * oracle keeps per-tab — its own field, its own mutations, its own error copy — and
 * NEVER navigates. On a verify response it calls `onAuthResult` with the RAW body
 * and stops: the shell's single `authDispositionOf` (`../auth-result`) owns the MFA
 * branches, the open-redirect guard and the ticket exchange. Three panels
 * re-deriving that table would be three chances to disagree about where a
 * half-authenticated user lands.
 *
 * Testids come verbatim from the oracle: `login-magic-link-email`,
 * `login-magic-link-submit`, `login-magic-link-sent`. Errors go to the shell's ONE
 * shared `login-error` banner via `onError`.
 *
 * ── THE TWO HALVES OF THIS TAB ───────────────────────────────────────────────
 *
 * SEND: the user types an address and the API emails them a link. `returnUrl` and
 *   `client_id` ride along so `MagicLinkRequestedNotificationHandler.cs:21-31` can
 *   put them back on the emailed link and land the user in the OIDC flow they left.
 *
 * VERIFY: the user clicks that link, arrives at `/login?magicLinkToken=…`, and the
 *   token is redeemed ON LOAD — the oracle's `OnInitializedAsync` (:255-259), which
 *   also forces the tab. There is no button: the user already clicked it, in their
 *   inbox.
 *
 * ── WHY SEND IS A MUTATION AND VERIFY IS NOT (Wallow-x4qn.9.3) ────────────────
 *
 * `GET /passwordless/magic-link/verify` is a READ to the code generator, so
 * `@bc-solutions-coder/sdk/query` emits `accountVerifyMagicLinkOptions` and NO
 * `accountVerifyMagicLinkMutation`. Rather than hand-roll a wrapper back — the shape
 * this bead deletes everywhere else — the redemption runs through the query client
 * (`fetchQuery`), which is also the app's rule for reads. Two consequences the effect
 * below is written around: the response lands in the CACHE (so it must never be
 * served stale — see the latch note), and a failure REJECTS rather than arriving at
 * an `onError` callback (so the rejection is caught explicitly).
 *
 * ── WHY `returnUrl`/`clientId` ARE PROPS AND NOT IN `LoginPanelProps` ────────
 *
 * `SendMagicLinkRequest` carries them (`{ email, returnUrl?, clientId? }`) but
 * `SendOtpRequest` is `{ email }` alone, so they are THIS panel's inputs, not every
 * panel's. `../panel` stays the minimal contract all three tabs share.
 */

/** The oracle's magic-link email `BbInput`. */
function EmailField(props: { readonly value: string; readonly onChange: (v: string) => void }) {
  const { value, onChange } = props;

  return (
    <Field>
      <Label htmlFor="magicLinkEmail">Email</Label>
      <Input
        id="magicLinkEmail"
        type="email"
        placeholder="name@example.com"
        data-testid="login-magic-link-email"
        value={value}
        onChange={(e) => {
          onChange(e.target.value);
        }}
      />
    </Field>
  );
}

/** The oracle's send `BbButton`, with its `Loading`/`Disabled="_isSubmitting"`. */
function SubmitButton({ pending }: { readonly pending: boolean }) {
  return (
    <Button
      type="submit"
      // One click, one link. A double send spends the address's rate-limit
      // allowance and the second link silently invalidates nothing — but the
      // refusal it earns is a worse experience than a disabled button.
      disabled={pending}
      data-testid="login-magic-link-submit"
    >
      {pending ? "Sending..." : "Send link"}
    </Button>
  );
}

/**
 * The oracle's `_magicLinkSent` success `BbAlert`, which REPLACES the form: the
 * link is in the user's inbox, and a form still on screen invites a second send
 * the rate limiter will refuse.
 *
 * The copy is a CONSTANT and names neither the address nor the backend's answer.
 * The API returns the identical `200 { succeeded: true }` for an address with no
 * account (`PasswordlessService.cs:63-67`) precisely so this screen cannot be used
 * to enumerate users; the confirmation is the artefact both outcomes share and it
 * stays byte-identical (bd memory `anti-enumeration-pattern-for-endpoints-that-must-not`).
 */
function SentAlert() {
  return (
    <div
      className="rounded-md border border-success bg-success/10 p-3"
      data-testid="login-magic-link-sent"
    >
      <Text as="p" variant="bodySm">
        {MAGIC_LINK_SENT_MESSAGE}
      </Text>
    </div>
  );
}

export interface MagicLinkLoginFormProps extends LoginPanelProps {
  /**
   * The oracle's `[SupplyParameterFromQuery(Name = "magicLinkToken")]`, threaded
   * down by the route. Present ONLY when the user arrived from the emailed link.
   */
  readonly token?: string;
  /** Cargo for the send, so the emailed link can resume this OIDC flow. */
  readonly returnUrl?: string;
  /** Cargo for the send, alongside `returnUrl`. */
  readonly clientId?: string;
}

export function MagicLinkLoginForm({
  token,
  returnUrl,
  clientId,
  onAuthResult,
  onError,
}: MagicLinkLoginFormProps): ReactNode {
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();
  const [email, setEmail] = useState("");
  const [sent, setSent] = useState(false);
  /**
   * The oracle verifies ONCE, in `OnInitializedAsync`. A magic-link token is
   * ONE-TIME USE (`PasswordlessService.cs:117` deletes the Redis key on redemption),
   * so a second redemption can only ever fail — and would paint "this link has
   * expired" over a sign-in that just succeeded.
   *
   * A `useRef` latch, NOT effect-dep correctness: a failed verify sets the shell's
   * banner, which re-renders this panel with fresh `onAuthResult`/`onError`
   * identities, and React StrictMode double-invokes effects in dev for good measure
   * (bd memory `exactly-once-server-mutations-in-react-need-a-ref-not-just-deps`).
   * KNOWN TRADEOFF, per that memory: the ref masks a stray-dep regression, so the
   * "exactly once" test binds the OUTCOME, not the mechanism.
   *
   * The latch survives the move to `fetchQuery` and is MORE load-bearing there:
   * `queryClient` is stable across renders where the old mutation object was not,
   * which makes the effect merely LOOK dep-safe.
   */
  const verifyStartedRef = useRef(false);

  const sendMutation = useMutation(accountSendMagicLinkMutation({ client: sdk.client }));

  useEffect(() => {
    // The latch, READ FIRST. The failure arm sets the shell's banner, which
    // re-renders this panel with fresh callback identities — so without this the
    // effect re-fires, redeeming a one-time token that Redis has already deleted.
    if (verifyStartedRef.current) {
      return;
    }

    // `IsNullOrEmpty(MagicLinkToken)` parity: `""` is not nullish, and redeeming it
    // would spend a request to be told the format is invalid. Checked BEFORE the
    // latch is set, so a token arriving later is still redeemed once.
    if (token === undefined || token === "") {
      return;
    }

    verifyStartedRef.current = true;
    // The oracle's `_errorMessage = null` at the top of `HandleVerifyMagicLink`.
    onError(null);

    // No `rememberMe`: the oracle passes none, and the endpoint defaults it false
    // (AccountController.cs:840). A link mailed to an inbox is not a "trust this
    // device" signal. No `staleTime`/`initialData` either — see the header: a
    // replayed cache entry would hand back a `signInTicket` for a redemption that
    // never happened.
    void queryClient
      .fetchQuery(accountVerifyMagicLinkOptions({ client: sdk.client, query: { token } }))
      .then(
        // The RAW body goes up. Resolution is not, on its own, a destination: the
        // shell narrows it and decides (see `../panel`).
        onAuthResult,
        // A rejection, NOT an `onError` option: `fetchQuery` throws where
        // `mutate()` routed failure to a callback, and an uncaught rejection here
        // would leave the banner empty over a dead form.
        (error: unknown) => {
          onError(verifyMagicLinkFailureMessage(error));
        },
      );
  }, [token, onAuthResult, onError, queryClient, sdk]);

  const handleSubmit = (): void => {
    // The oracle's `IsNullOrWhiteSpace(_email)` guard — WHITEspace, so "   " is
    // blank. A blank send cannot succeed and would spend rate-limit allowance.
    if (email.trim() === "") {
      onError(BLANK_EMAIL_MESSAGE);
      return;
    }

    // The oracle's `_errorMessage = null` at the top of `HandleSendMagicLink`: a
    // stale banner hanging over an in-flight retry is a lie.
    onError(null);

    // The generated artifact's REQUEST object, not a bare body: `returnUrl` and
    // `clientId` ride along so the emailed link can resume this OIDC flow.
    sendMutation.mutate(
      { body: { email, returnUrl, clientId } },
      {
        onSuccess: (body: unknown) => {
          if (!magicLinkWasSent(body)) {
            // Fail closed: a body this screen cannot read is not a sent link, and
            // sending the user to watch an empty inbox is worse than an error.
            onError(GENERIC_MESSAGE);
            return;
          }

          setSent(true);
        },
        onError: (cause: unknown) => {
          // The form deliberately stays up — the user's address may simply have
          // been mistyped, and they need somewhere to fix it.
          onError(sendMagicLinkFailureMessage(cause));
        },
      },
    );
  };

  if (sent) {
    // The oracle's `SwitchTab` also resets `_magicLinkSent`. Here that is free: the
    // flag is panel-local state and switching tabs unmounts the panel, so the shell
    // needs no reset it would otherwise have to grow.
    return <SentAlert />;
  }

  return (
    <form
      className="space-y-4"
      onSubmit={(e) => {
        e.preventDefault();
        e.stopPropagation();
        handleSubmit();
      }}
    >
      <EmailField value={email} onChange={setEmail} />
      <SubmitButton pending={sendMutation.isPending} />
    </form>
  );
}
