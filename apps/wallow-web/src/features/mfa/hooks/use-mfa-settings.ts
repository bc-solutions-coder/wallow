/**
 * The MFA settings card's whole state machine: the status read, the
 * disable/regenerate writes, and the five pieces of local state the card's
 * branches are chosen by.
 *
 * It exists so `MfaSettingsSection` can be read as a layout — the card was 331
 * lines in which the enrolment toggle, the confirm panel's password, the two
 * mutations' shared error surface and the one-time codes reveal were interleaved
 * with the JSX that renders them.
 *
 * Both failure surfaces come back as the RAW failure rather than as text:
 * deciding what a failure says is the shared `FailureBanner`'s job (the app's
 * registry resolves the MFA controllers' `{ succeeded: false, error }` codes
 * the same way it resolves any RFC 7807 problem). What this layer decides is
 * WHICH surface a failure belongs to — the status READ's branch or the two
 * WRITES' banner.
 */

import { handledFailure, useMutation, useQuery, useQueryClient } from "@bc-solutions-coder/query";
import { useRouteContext } from "@tanstack/react-router";
import { useState } from "react";

import {
  mfaDisableMutation,
  mfaGetStatusOptions,
  mfaGetStatusQueryKey,
  mfaRegenerateBackupCodesMutation,
  queriesForOperation,
} from "../api";

/** Which enabled-only action opened the shared password-confirm panel. */
type ConfirmAction = "disable" | "regenerate";

/**
 * Await a `mutate` call, resolving whichever way it lands.
 *
 * `mutateAsync` would be the obvious reach and is the wrong one: it REJECTS on
 * failure, and the failure here is already reported on the card's own
 * `settings-mfa-error` banner — a rejection propagating into the confirm form
 * would raise a second banner saying the same thing in the form's words.
 */
function settled(run: (onSettled: () => void) => void): Promise<void> {
  return new Promise((resolve) => {
    run(() => {
      resolve();
    });
  });
}

/** What {@link useMfaSettings} hands the card. */
export interface MfaSettings {
  /** The status read has not answered yet — the card renders its loading state. */
  readonly isPending: boolean;
  /**
   * The status read's failure when it failed with nothing cached to fall back
   * on; `null` otherwise.
   *
   * Distinct from {@link error}, which is the two WRITES' surface: a card that
   * could not read its status must not claim MFA is off and invite a second
   * enrolment, so this is a branch rather than a banner.
   */
  readonly statusError: unknown;
  /** Re-run the status read after {@link statusError}. */
  readonly refetchStatus: () => unknown;
  readonly enabled: boolean;
  /** The API types this as `number | string`; it is relayed, never cast. */
  readonly backupCodeCount: number | string;
  /** The inline enrol flow has replaced the card. */
  readonly enrolling: boolean;
  /** Which action the open confirm panel will run, or `null` when it is closed. */
  readonly confirmAction: ConfirmAction | null;
  /**
   * The disable/regenerate failure surface: the failure the last confirm threw,
   * or `null` when the last one succeeded or none has run.
   */
  readonly error: unknown;
  /** Freshly minted codes to reveal once, or `null` when there are none to show. */
  readonly regeneratedCodes: readonly string[] | null;
  readonly beginEnroll: () => void;
  readonly endEnroll: () => void;
  readonly openConfirm: (action: ConfirmAction) => void;
  /**
   * Runs the open panel's action with `password`.
   *
   * Settles rather than resolving immediately, and never REJECTS: the confirm
   * form awaits it for its own `pending`, while the failure itself is reported
   * through {@link error} on the card's own banner rather than the form's.
   */
  readonly submitConfirm: (password: string) => Promise<void>;
}

export function useMfaSettings(): MfaSettings {
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();
  const {
    data,
    isPending,
    isError,
    error: statusError,
    refetch: refetchStatus,
  } = useQuery(mfaGetStatusOptions({ client: sdk.client }));

  const [enrolling, setEnrolling] = useState(false);
  const [confirmAction, setConfirmAction] = useState<ConfirmAction | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [regeneratedCodes, setRegeneratedCodes] = useState<string[] | null>(null);

  // Both writes change what the card reports (enrolment state and the remaining
  // count), so each re-reads the status OPERATION. Generated keys are flat, so
  // there is no `['mfa']` prefix to sweep by; the status query's `Identity` tag
  // is far broader than these two writes touch.
  const invalidateStatus = (): void => {
    void queryClient.invalidateQueries(
      queriesForOperation(mfaGetStatusQueryKey({ client: sdk.client })),
    );
  };
  // The confirm panel owns these failures; the root toaster stays quiet.
  const disable = useMutation({
    ...mfaDisableMutation({ client: sdk.client }),
    meta: handledFailure(),
    onSuccess: invalidateStatus,
  });
  const regenerate = useMutation({
    ...mfaRegenerateBackupCodesMutation({ client: sdk.client }),
    meta: handledFailure(),
    onSuccess: invalidateStatus,
  });

  const openConfirm = (action: ConfirmAction): void => {
    // Opening either panel clears the LAST attempt's residue — its error and any
    // codes still on screen. Codes especially: they belong to the regenerate
    // that produced them, and leaving them up beside a fresh prompt reads as if
    // they were about to be reissued. The typed password is not cleared here
    // because it is no longer held here: the card keys the confirm form by
    // action, so switching panels mounts a fresh, empty one.
    setError(null);
    setRegeneratedCodes(null);
    setConfirmAction(action);
  };

  const submitConfirm = async (password: string): Promise<void> => {
    if (confirmAction === null) {
      return;
    }
    setError(null);

    // Named `failure` rather than `error` because the state it writes into is
    // already called that, and `eslint/no-shadow` is on.
    const onError = (failure: unknown): void => {
      // The panel deliberately stays OPEN on failure: the likeliest cause is a
      // mistyped password, and closing it would make the user reopen the flow to
      // retry.
      setError(failure);
    };
    const closePanel = (): void => {
      setConfirmAction(null);
    };

    if (confirmAction === "disable") {
      await settled((onSettled) => {
        disable.mutate({ body: { password } }, { onSuccess: closePanel, onError, onSettled });
      });
      return;
    }

    await settled((onSettled) => {
      regenerate.mutate(
        { body: { password } },
        {
          onSuccess: (payload) => {
            // Revealed once, because regenerating invalidated the old codes. The
            // mutation's own `onSuccess` re-reads the status, so the card stays
            // Enabled with the new count.
            setRegeneratedCodes(payload.codes);
            closePanel();
          },
          onError,
          onSettled,
        },
      );
    });
  };

  return {
    isPending,
    // A cached status surviving a failed refetch is still the truth as of the
    // last successful read, so only a failure with NOTHING to render is fatal.
    statusError: isError && data === undefined ? statusError : null,
    refetchStatus,
    enabled: data?.enabled ?? false,
    backupCodeCount: data?.backupCodeCount ?? 0,
    enrolling,
    confirmAction,
    error,
    regeneratedCodes,
    beginEnroll: () => {
      setEnrolling(true);
    },
    endEnroll: () => {
      setEnrolling(false);
    },
    openConfirm,
    submitConfirm,
  };
}
