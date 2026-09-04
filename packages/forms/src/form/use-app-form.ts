/**
 * `useAppForm` — the one hook a Wallow form calls.
 *
 * It unifies the four things every hand-written form in the apps repeats today:
 * a TanStack Form instance, a zod schema wired as the submit validator, the
 * TanStack Query mutation the submit drives, and the failure split that decides
 * which messages go on a field and which failure the banner resolves its
 * sentence from. The result is the plain TanStack form instance augmented with
 * a `wallow` member (`pending`/`serverError`/`reset`), which `AppForm` reads so
 * a call site does not have to thread either through props.
 *
 * A form is a handled failure surface: every mutation the hook creates carries
 * `handledFailure` in its `meta`, so the query client's `onUnhandledFailure`
 * callback never toasts what the form already shows.
 */

import type { ApiFailure, FailureMessageRegistry } from "@bc-solutions-coder/api-errors";
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
  /** Drops the mutation's result/error state, e.g. when a dialog reopens. */
  readonly reset: () => void;
  /**
   * Drops the last submit's server errors — the banner and the field messages
   * pushed under the `onServer` key.
   *
   * `AppForm` calls this on its way into a submit, and it has to happen THERE
   * rather than inside this hook's `onSubmit`: `handleSubmit` aborts on
   * `!isFieldsValid` before the submit callback runs, and nothing in
   * `@tanstack/form-core` ever clears an `onServer` error by itself (it is
   * untouched by `validateSync`, which only rewrites the key for the cause it
   * validated). A server field error left in place would therefore wedge the
   * form: every later submit would fail the gate silently.
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
 * The TanStack form instance for values `TValues`, augmented with
 * {@link WallowFormExtras}. Augmenting the instance rather than returning a
 * tuple is `createFormHook`'s own pattern, and it keeps `form.AppField`,
 * `form.Field` and `form.handleSubmit` exactly where a TanStack user expects.
 *
 * The validator generics are pinned to the one shape this hook ever builds: a
 * standard-schema `onDynamic` validator (the caller's zod schema) and nothing
 * else. `FormApi` declares them `in out`, so an approximation would not be
 * assignable — they have to match the instantiation below exactly. The schema
 * sits in the `TOnDynamic` slot rather than `TOnSubmit` because
 * {@link revalidateLogic} runs only that one validator; see the instantiation
 * below for why.
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

export interface UseAppFormOptions<TValues, TVariables, TData, TError = unknown> {
  /**
   * The zod schema, wired as TanStack's `validators.onDynamic` under
   * {@link revalidateLogic} — so it runs on submit, and thereafter on every
   * change. It is typed as the standard-schema interface zod implements, which
   * is what TanStack itself accepts; `TValues` is the schema's input type, i.e.
   * the shape the form holds.
   */
  readonly schema: StandardSchemaV1<TValues, unknown>;
  readonly defaultValues: TValues;
  /**
   * The generated SDK mutation options (`{operation}Mutation({ client })`),
   * passed WHOLE — no destructuring, no cast. Omit it for the plain-`onSubmit`
   * escape hatch.
   *
   * `TError` is inferred from whatever is handed over, which is the only way the
   * generated factories can be accepted at all: each operation carries its own
   * error type (`organizationsCreateMutation` is `DefaultError`,
   * `organizationClientsRegisterMutation` is
   * `OrganizationClientsRegisterError`), and `TError` sits in the
   * CONTRAVARIANT position of `UseMutationOptions`' optional
   * `onError`/`onSettled`/`retry` members — so a slot pinned to any one concrete
   * type (`unknown` included) rejects every factory that does not name exactly
   * that type. The hook never reads `TError` itself; `splitSubmitFailure` takes
   * the failure as `unknown` because an RFC 7807 body is only trustworthy after
   * the runtime classification it does.
   */
  readonly mutation?: UseMutationOptions<TData, TError, TVariables>;
  /** Values -> mutation variables. Defaults to `(values) => ({ body: values })`. */
  readonly toVariables?: (values: TValues) => TVariables;
  /**
   * The no-mutation escape hatch (e.g. the forgot-password screen, which
   * deliberately swallows failures for anti-enumeration). It still runs through
   * an internal mutation so `pending` keeps working.
   */
  readonly onSubmit?: (values: TValues) => Promise<void> | void;
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

export function useAppForm<TValues, TVariables = unknown, TData = unknown, TError = unknown>(
  options: UseAppFormOptions<TValues, TVariables, TData, TError>,
): AppFormApi<TValues> {
  const [bannerFailure, setBannerFailure] = useState<ApiFailure | null>(null);
  /*
   * The banner is a failure message like any other surface's: resolved through
   * the app registry the `FailureMessagesProvider` publishes (the shipped copy
   * without one), with this form's `messages` ahead of it and `fallbackError`
   * behind. Resolving here rather than in the mutation callback is what lets
   * the registry reach it — the callback runs outside React.
   */
  const serverError: string | null = useFailureMessage(bannerFailure, {
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
    /*
     * WHEN validation runs, and the reason the schema is an `onDynamic`
     * validator rather than an `onSubmit` one.
     *
     * `revalidateLogic()` defaults to `mode: "submit"` /
     * `modeAfterSubmission: "change"`, which is exactly the rule every Wallow
     * form wants: a first-time visitor is never judged mid-keystroke, and a
     * field the submit has already flagged then tracks the value live, so the
     * message clears the moment it is fixed instead of waiting for a second
     * submit. It is deliberately set HERE and not per form — the five migrated
     * screens configure none of this.
     *
     * The strategy runs ONLY the `onDynamic` validator (it ignores
     * `onChange`/`onBlur`/`onSubmit` entirely), so leaving the schema on
     * `onSubmit` would silently validate nothing at all.
     */
    validationLogic: revalidateLogic(),
    validators: { onDynamic: options.schema },
    onSubmit: ({ value }) => {
      // A banner must not outlive the submit that produced it, and a submit
      // reached programmatically skips the shell's own clear.
      setBannerFailure(null);

      mutation.mutate(toMutationVariables(options, value), {
        onSuccess: (data: TData) => {
          options.onSuccess?.(data);
        },
        onError: (error: unknown) => {
          const split: SubmitFailure = splitSubmitFailure(
            error,
            Object.keys(options.defaultValues as Record<string, unknown>),
          );

          setBannerFailure(split.bannerFailure);
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
    setBannerFailure(null);
    form.setErrorMap({ onServer: { fields: {} } });
  }, [form]);

  const reset = useCallback((): void => {
    mutation.reset();
    clearServerErrors();
  }, [mutation, clearServerErrors]);

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
