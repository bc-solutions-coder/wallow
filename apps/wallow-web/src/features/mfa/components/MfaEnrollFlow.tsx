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
 * Testids mirror the C# E2E page object `MfaEnrollPage`.
 */
import { Button, Card, CardTitle, ErrorBanner, Field, Input, Label } from "@bc-solutions-coder/ui";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useRouteContext } from "@tanstack/react-router";
import { useState } from "react";

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

/** The setup CTA (initial state): clicking it mints the one-time secret + QR. */
function SetupStep(props: { onBegin: () => void }) {
  return (
    <Button type="button" data-testid="mfa-enroll-begin-setup" onClick={props.onBegin}>
      Begin setup
    </Button>
  );
}

/** The verify step: shows the secret + QR and collects the confirmation code. */
function VerifyStep(props: {
  secret: string;
  qrUri: string;
  code: string;
  onCodeChange: (value: string) => void;
  onSubmit: () => void;
}) {
  const { secret, qrUri, code, onCodeChange, onSubmit } = props;
  return (
    <div>
      <span data-testid="mfa-enroll-secret">{secret}</span>
      <div data-testid="mfa-enroll-qr">
        <code>{qrUri}</code>
      </div>
      <Field>
        <Label htmlFor="mfa-enroll-code-input">Verification code</Label>
        <Input
          id="mfa-enroll-code-input"
          data-testid="mfa-enroll-code"
          value={code}
          onChange={(e) => {
            onCodeChange(e.target.value);
          }}
        />
      </Field>
      <Button type="button" data-testid="mfa-enroll-submit" onClick={onSubmit}>
        Verify
      </Button>
    </div>
  );
}

/** The done step: the one-time backup codes reveal (one child per code) + Done. */
function DoneStep(props: { codes: string[]; onDone: () => void }) {
  const { codes, onDone } = props;
  return (
    <div>
      <p>Save your backup codes now. They will not be shown again.</p>
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
  const queryClient = useQueryClient();
  // Enrolling only mints a one-time secret, so it invalidates nothing; confirming
  // is what flips the status the settings card renders, and it re-reads that one
  // operation (generated keys are flat, and the status query's `Identity` tag is
  // far broader than this write touches).
  const enroll = useMutation(mfaEnrollTotpMutation({ client: sdk.client }));
  const confirm = useMutation({
    ...mfaConfirmEnrollmentMutation({ client: sdk.client }),
    onSuccess: (): void => {
      void queryClient.invalidateQueries(
        queriesForOperation(mfaGetStatusQueryKey({ client: sdk.client })),
      );
    },
  });

  const [secret, setSecret] = useState<string | null>(null);
  const [qrUri, setQrUri] = useState<string | null>(null);
  const [code, setCode] = useState("");
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

  const handleSubmit = () => {
    if (secret === null) {
      return;
    }
    setError(null);
    confirm.mutate(
      { body: { secret, code } },
      {
        onSuccess: (data) => {
          // A rejected confirmation no longer resolves: the SDK's error
          // interceptor turns the endpoint's `{ succeeded: false, error }`
          // BadRequest into a thrown `WallowError`, so reaching here means the
          // enrollment took. One-time reveal: hold the codes locally and drop
          // the secret so the verify step is gone; the mutation's own onSuccess
          // re-reads the status so the card flips to Enabled.
          //
          // The `??` survives the move to the generated type even though that
          // type declares `backupCodes` REQUIRED: the declaration is the
          // schema's claim, not the wire's. A null list serializes to an absent
          // member, and `DoneStep` maps over what it is given — so an enrollment
          // that WORKED would blow up on its own success screen.
          setBackupCodes(data.backupCodes ?? []);
          setSecret(null);
        },
        onError: (err) => {
          setError(problemDetail(err, CONFIRM_FAILED));
        },
      },
    );
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
          code={code}
          onCodeChange={setCode}
          onSubmit={handleSubmit}
        />
      );
    }
    return <SetupStep onBegin={handleBegin} />;
  }
}
