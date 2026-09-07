/**
 * Combine schema validation, mutation state, and field or banner failures. Form mutations are
 * marked handled so the query client does not report the same failure again.
 */

import type { FailureMessageRegistry } from "@bc-solutions-coder/api-errors";
import { handledFailure, useMutation, type UseMutationOptions } from "@bc-solutions-coder/query";
import { useFailureMessage } from "@bc-solutions-coder/ui/failure-messages";
import {
  type FormValidateAsyncFn,
  revalidateLogic,
  type StandardSchemaV1,
} from "@tanstack/react-form";
import { useCallback, useState } from "react";

import { useTanstackAppForm } from "../core/form-hook";
import { splitSubmitFailure, type SubmitFailure } from "../core/server-error";

/** What `useAppForm` adds to the TanStack form instance it returns. */
export interface WallowFormExtras {
  /** Whether the submit mutation is in flight — `AppForm` publishes it as `pending`. */
  readonly pending: boolean;
  /** The form-level failure text `FormError` renders, or `null`. */
  readonly serverError: string | null;
  /**
   * Clear mutation result and failure state, including server field messages and banner text.
   * Does not reset form values or schema errors; use form.reset() for form values.
   */
  readonly reset: () => void;
  /**
   * Clear the banner and onServer field errors without resetting values. AppForm calls this
   * before validation; callers invoking handleSubmit directly must do the same before retrying a
   * server-invalid form.
   */
  readonly clearServerErrors: () => void;
}

/**
 * The `TOnServer` slot of a form instance, which decides the type of the
 * `onServer` error-map key.
 *
 * TanStack derives that key's type from the *return* of a server validator and,
 * unlike `onSubmit`/`onChange`, does not union in `GlobalFormValidationError`
 * (see `FormValidationErrorMap` in `@tanstack/form-core`). Left at `undefined`
 * the key types as `undefined`, and the very call the runtime supports —
 * `setErrorMap({ onServer: { fields } })`, which `FormApi` duck-types on the
 * `fields` member — would not compile. Naming the slot as a validator returning
 * `unknown` restores it without loosening anything the form actually validates:
 * no server validator is ever passed.
 */
type ServerErrorSlot<TValues> = FormValidateAsyncFn<TValues>;

/**
 * TanStack form instance with the registered AppField catalog and Wallow submit state. Its schema
 * occupies onDynamic, and onServer holds errors returned by a submission.
 */
export type AppFormApi<TValues> = ReturnType<
  typeof useTanstackAppForm<
    TValues,
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    StandardSchemaV1<TValues, unknown>,
    undefined,
    ServerErrorSlot<TValues>,
    never
  >
> & {
  readonly wallow: WallowFormExtras;
};

/**
 * Schema, initial values, and submit behavior. Provide a mutation or an onSubmit callback;
 * mutation takes precedence when both are supplied.
 */
export interface UseAppFormOptions<TValues, TVariables, TData, TError = unknown> {
  /**
   * Standard Schema validator, such as a Zod schema. Validates on the first submit and on changes
   * afterward. The form stores and submits the input values; schema output transformations are
   * not applied to mutation variables.
   */
  readonly schema: StandardSchemaV1<TValues, unknown>;
  /** Initial form values and the known field names used to match server errors. */
  readonly defaultValues: TValues;
  /**
   * Mutation options, including a generated SDK mutation factory result. Passed through with
   * failureHandled metadata added. The error type is inferred from the supplied options; omit
   * mutation to use onSubmit.
   */
  readonly mutation?: UseMutationOptions<TData, TError, TVariables>;
  /** Convert values to mutation variables. Defaults to { body: values } with mutation, or values with onSubmit. */
  readonly toVariables?: (values: TValues) => TVariables;
  /**
   * Submission callback used only when mutation is absent. Runs through an internal mutation to
   * provide pending and failure state. Receives form values unless toVariables replaces them.
   */
  readonly onSubmit?: (values: TValues) => Promise<void> | void;
  /** Called after a successful submit with mutation data, or undefined for the onSubmit callback path. */
  readonly onSuccess?: (data: TData) => void;
  /**
   * Banner sentences for this form alone, keyed by error code. They win over
   * the app registry and the shipped copy — the place for wording that only
   * makes sense on this screen.
   */
  readonly messages?: FailureMessageRegistry | undefined;
  /**
   * The banner's last resort, ahead of the shipped generic sentence, for a
   * failure no code, status, or detail covers. Most forms need none.
   */
  readonly fallbackError?: string | undefined;
}

/**
 * Create a form with schema validation and a handled submission mutation under
 * QueryClientProvider. AppForm consumes form.wallow for pending state and resolved banner text.
 * API field errors match keys in defaultValues; unmatched messages remain available to the banner
 * resolver.
 */
export function useAppForm<TValues, TVariables = unknown, TData = unknown, TError = unknown>(
  options: UseAppFormOptions<TValues, TVariables, TData, TError>,
): AppFormApi<TValues> {
  const [submitFailure, setSubmitFailure] = useState<SubmitFailure | null>(null);
  /*
   * The banner is a failure message like any other surface's: resolved through
   * the app registry the `FailureMessagesProvider` publishes (the shipped copy
   * without one), with this form's `messages` ahead of it and `fallbackError`
   * behind. Resolving here rather than in the mutation callback is what lets
   * the registry reach it — the callback runs outside React.
   */
  const serverError: string | null = useFailureMessage(submitFailure?.bannerFailure, {
    unmatched: submitFailure?.unmatched,
    messages: options.messages,
    fallback: options.fallbackError,
  });

  /*
   * Exactly one `useMutation` on every path (rules of hooks). With no SDK
   * mutation the escape hatch supplies a stand-in whose variables ARE the form
   * values — see `toMutationVariables` — so `pending` and the failure split
   * behave identically whether or not an operation is involved. Either way the
   * mutation is marked handled: the form shows its own failure, so the query
   * client's callback must not toast it too.
   */
  const mutation = useMutation<TData, TError, TVariables>({
    ...(options.mutation ?? {
      mutationFn: async (variables: TVariables): Promise<TData> => {
        await options.onSubmit?.(variables as unknown as TValues);

        return undefined as TData;
      },
    }),
    meta: handledFailure(options.mutation?.meta),
  });

  const form = useTanstackAppForm<
    TValues,
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    StandardSchemaV1<TValues, unknown>,
    undefined,
    ServerErrorSlot<TValues>,
    never
  >({
    defaultValues: options.defaultValues,
    /**
     * revalidateLogic runs onDynamic validation on submit, then on changes after submission.
     * Putting the schema under onSubmit would bypass this strategy.
     */
    validationLogic: revalidateLogic(),
    validators: { onDynamic: options.schema },
    onSubmit: ({ value }) => {
      // A banner must not outlive the submit that produced it, and a submit
      // reached programmatically skips the shell's own clear.
      setSubmitFailure(null);

      mutation.mutate(toMutationVariables(options, value), {
        onSuccess: (data: TData) => {
          options.onSuccess?.(data);
        },
        onError: (error: unknown) => {
          const split: SubmitFailure = splitSubmitFailure(
            error,
            Object.keys(options.defaultValues as Record<string, unknown>),
          );

          setSubmitFailure(split);
          /*
           * `onServer` is the error-map key for messages that came from outside
           * the form, and the framework hands each already-mounted field
           * `fields[name]` verbatim — `fieldMetaDerived` flattens the map one
           * level, so a plain string array lands as separate entries in
           * `field.state.meta.errors`. No issue-object wrapping is involved.
           * (The form-level half stays out of the map: it is resolved into
           * `serverError` above, and an `onServer` form error would also fail
           * the next submit's validity gate.)
           */
          form.setErrorMap({ onServer: { fields: split.fieldErrors } });
        },
      });
    },
  });

  const clearServerErrors = useCallback((): void => {
    setSubmitFailure(null);
    form.setErrorMap({ onServer: { fields: {} } });
  }, [form]);

  // `mutation` is a fresh object every render; its bound `reset` is stable.
  const { reset: resetMutation } = mutation;
  const reset = useCallback((): void => {
    resetMutation();
    clearServerErrors();
  }, [resetMutation, clearServerErrors]);

  return Object.assign(form, {
    wallow: {
      pending: mutation.isPending,
      serverError,
      reset,
      clearServerErrors,
    },
  });
}

/**
 * The variables one submit hands the mutation.
 *
 * The default wraps the values as `{ body: values }`, which is the request
 * options object every generated `{operation}Mutation` takes; `toVariables`
 * replaces it for the operations that also carry a path parameter. On the
 * escape hatch the values themselves are the variables — nothing else supplies
 * them, so the stand-in `mutationFn` above can read them back as `TValues`.
 */
function toMutationVariables<TValues, TVariables, TData, TError>(
  options: UseAppFormOptions<TValues, TVariables, TData, TError>,
  values: TValues,
): TVariables {
  if (options.toVariables) {
    return options.toVariables(values);
  }

  if (options.mutation) {
    return { body: values } as TVariables;
  }

  return values as unknown as TVariables;
}
