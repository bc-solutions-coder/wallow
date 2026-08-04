/**
 * MFA enroll flow component (Wallow-8w1h.6.4) — the multi-step TOTP enrollment
 * UX: a single component driving a small step machine. In wallow-web the whole
 * SPA is same-origin via the BFF
 * proxy, so there is NO cross-app EnrollToken redirect dance — this component
 * calls the generated `mfaEnrollTotp` operation directly.
 *
 * STEP MACHINE (strict order, derived from local state):
 *   "setup"  — the `mfa-enroll-begin-setup` CTA is shown; clicking it runs the
 *              `enrollTotp` mutation to mint the one-time secret + QR.
 *   "verify" — once the secret exists, `mfa-enroll-secret` + `mfa-enroll-qr`
 *              (QR from the `qrUri`) + `mfa-enroll-code` (input) + `mfa-enroll-
 *              submit` are shown; submit runs the `confirmEnroll` mutation.
 *   "done"   — once a confirm RESOLVES, the one-time
 *              `mfa-enroll-backup-codes` (one child per code) are revealed ONCE
 *              with a Done action; status is invalidated so the card flips to
 *              Enabled.
 *
 * `mfa-enroll-error` surfaces any step's failure. There is no resolved-but-
 * rejected branch left: every MFA failure — RFC 7807 body or the controller's
 * raw `{ succeeded: false, error }` — arrives as a thrown `WallowError`, which
 * `problemDetail` renders. `mfa-enroll-cancel` is always visible.
 *
 * ONLY THE VERIFY STEP IS A FORM. "Begin setup" is a button that mints a secret,
 * not a submit — it collects nothing, so there is nothing to validate and nothing
 * to hold. The confirm step collects exactly one value and submits it, which is a
 * form, and it owns its own `useAppForm` (see `VerifyStep`) so that the secret it
 * needs is a non-null PROP rather than a nullable read the submit has to re-guard.
 *
 * Testids mirror the C# E2E page object `MfaEnrollPage`.
 */
import { AppForm, SubmitButton, useAppForm } from "@bc-solutions-coder/forms";
import { useMutation, useQueryClient } from "@bc-solutions-coder/query";
import { Button, Card, CardTitle, ErrorBanner, Text } from "@bc-solutions-coder/ui";
import { useRouteContext } from "@tanstack/react-router";
import { useState } from "react";
import { z } from "zod";

import {
  mfaConfirmEnrollmentMutation,
  mfaEnrollTotpMutation,
  mfaGetStatusQueryKey,
  queriesForOperation,
} from "../api";
import { problemDetail } from "../errors";

/** Props: `onDone` fires after the backup codes are acknowledged; `onCancel` backs out. */
export interface MfaEnrollFlowProps {
  onDone?: () => void;
  onCancel?: () => void;
}

/** Fallback copy when a thrown error carries neither a ProblemDetails `detail` nor a known code. */
const ENROLL_FAILED = "Unable to start MFA enrollment.";
const CONFIRM_FAILED = "That verification code is not valid.";

/**
 * The one value the verify step collects. Its NAME is load-bearing: the
 * `mfa-enroll` prefix plus `code` is what derives the `mfa-enroll-code` testid the
 * E2E page object binds.
 *
 * RULE-FREE on purpose. A blank code is refused by the endpoint like any other
 * wrong one, and every failure this step can produce belongs on the CARD's shared
 * `mfa-enroll-error` banner — which is also where the enroll step's failures land,
 * and which a zod rule could not reach.
 */
const confirmSchema = z.object({ code: z.string() });

type ConfirmValues = z.infer<typeof confirmSchema>;

const NO_CODE: ConfirmValues = { code: "" };

/** The setup CTA (initial state): clicking it mints the one-time secret + QR. */
function SetupStep(props: { onBegin: () => void }) {
  return (
    <Button type="button" data-testid="mfa-enroll-begin-setup" onClick={props.onBegin}>
      Begin setup
    </Button>
  );
}

/**
 * The verify step: shows the secret + QR and collects the confirmation code.
 *
 * The confirm mutation is handed to `useAppForm` WHOLE — spread so this step can
 * add the invalidation and the two callbacks, never destructured or cast, because
 * the generated factory's own error type has to survive inference. Its `onError`
 * routes to the card's banner rather than the form's own, so no `FormError` is
 * rendered here: one enrollment card, one error surface, whichever step failed.
 *
 * Invalidation goes through `queriesForOperation` on the status key, NOT the
 * `Identity` tag: generated keys are flat, and the tag sweeps far more than this
 * write touches. Enrolling invalidates nothing at all — a minted secret is not yet
 * a status — so only the confirm carries it.
 */
function VerifyStep(props: {
  secret: string;
  qrUri: string;
  onAttempt: () => void;
  onConfirmed: (backupCodes: string[]) => void;
  onFailed: (message: string) => void;
}) {
  const { secret, qrUri, onAttempt, onConfirmed, onFailed } = props;
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();

  const form = useAppForm({
    schema: confirmSchema,
    defaultValues: NO_CODE,
    mutation: {
      ...mfaConfirmEnrollmentMutation({ client: sdk.client }),
      // The card's banner is cleared as the write STARTS, not when it succeeds: a
      // stale message hanging over an in-flight retry is a lie about the current
      // attempt.
      onMutate: (): void => {
        onAttempt();
      },
      onSuccess: (): void => {
        void queryClient.invalidateQueries(
          queriesForOperation(mfaGetStatusQueryKey({ client: sdk.client })),
        );
      },
      // Typed as the factory's OWN error type, not `unknown`: `TError` is
      // inferred from this object as a whole and sits contravariantly in
      // `throwOnError` too, so widening it here would reject the very factory
      // being spread. `problemDetail` still takes it as `unknown` — an RFC 7807
      // body is only trustworthy after the narrowing it does.
      onError: (cause: Error): void => {
        onFailed(problemDetail(cause, CONFIRM_FAILED));
      },
    },
    // The secret is a PROP, so the submit re-guards nothing: this step does not
    // exist until one has been minted.
    toVariables: (values: ConfirmValues) => ({ body: { secret, code: values.code } }),
    onSuccess: (data): void => {
      // A rejected confirmation no longer resolves: the SDK's error interceptor
      // turns the endpoint's `{ succeeded: false, error }` BadRequest into a
      // thrown `WallowError`, so reaching here means the enrollment took.
      //
      // The `??` survives the move to the generated type even though that type
      // declares `backupCodes` REQUIRED: the declaration is the schema's claim,
      // not the wire's. A null list serializes to an absent member, and the done
      // step maps over what it is given — so an enrollment that WORKED would blow
      // up on its own success screen.
      onConfirmed(data.backupCodes ?? []);
    },
  });

  return (
    <div>
      <Text as="span" variant="body" data-testid="mfa-enroll-secret">
        {secret}
      </Text>
      <div data-testid="mfa-enroll-qr">
        <Text as="code">{qrUri}</Text>
      </div>
      <AppForm form={form} testIdPrefix="mfa-enroll">
        <form.AppField name="code">
          {(field) => (
            <field.TextField
              label="Verification code"
              // A TOTP code is zero-padded six digits, so `type="number"` would
              // eat a leading zero; the digits-only hint travels separately.
              inputMode="numeric"
              autoComplete="one-time-code"
            />
          )}
        </form.AppField>
        <SubmitButton pendingLabel="Verifying…">Verify</SubmitButton>
      </AppForm>
    </div>
  );
}

/** The done step: the one-time backup codes reveal (one child per code) + Done. */
function DoneStep(props: { codes: string[]; onDone: () => void }) {
  const { codes, onDone } = props;
  return (
    <div>
      <Text as="p" variant="body">
        Save your backup codes now. They will not be shown again.
      </Text>
      <ul data-testid="mfa-enroll-backup-codes">
        {codes.map((codeValue) => (
          <li key={codeValue}>{codeValue}</li>
        ))}
      </ul>
      <Button type="button" data-testid="mfa-enroll-done" onClick={onDone}>
        Done
      </Button>
    </div>
  );
}

export function MfaEnrollFlow(props: MfaEnrollFlowProps) {
  const { onDone, onCancel } = props;
  const { sdk } = useRouteContext({ from: "__root__" });
  const enroll = useMutation(mfaEnrollTotpMutation({ client: sdk.client }));

  const [secret, setSecret] = useState<string | null>(null);
  const [qrUri, setQrUri] = useState<string | null>(null);
  const [backupCodes, setBackupCodes] = useState<string[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const handleBegin = () => {
    setError(null);
    enroll.mutate(
      {},
      {
        onSuccess: (data) => {
          setSecret(data.secret);
          setQrUri(data.qrUri);
        },
        onError: (err) => {
          setError(problemDetail(err, ENROLL_FAILED));
        },
      },
    );
  };

  const handleAttempt = () => {
    setError(null);
  };

  const handleConfirmed = (codes: string[]) => {
    // One-time reveal: hold the codes locally and drop the secret so the verify
    // step is gone. The form's own onSuccess re-read the status, so the settings
    // card behind this one has already flipped to Enabled.
    setBackupCodes(codes);
    setSecret(null);
  };

  const handleDone = () => {
    onDone?.();
  };

  const handleCancel = () => {
    onCancel?.();
  };

  return (
    <Card>
      <CardTitle>Set up two-factor authentication</CardTitle>
      {renderStep()}
      {error === null ? null : <ErrorBanner data-testid="mfa-enroll-error">{error}</ErrorBanner>}
      <Button
        type="button"
        variant="secondary"
        data-testid="mfa-enroll-cancel"
        onClick={handleCancel}
      >
        Cancel
      </Button>
    </Card>
  );

  function renderStep() {
    if (backupCodes !== null) {
      return <DoneStep codes={backupCodes} onDone={handleDone} />;
    }
    if (secret !== null) {
      return (
        <VerifyStep
          secret={secret}
          qrUri={qrUri ?? ""}
          onAttempt={handleAttempt}
          onConfirmed={handleConfirmed}
          onFailed={setError}
        />
      );
    }
    return <SetupStep onBegin={handleBegin} />;
  }
}
