// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Composable for async field validation (e.g. email availability).
 *
 * Generic — works for any field that needs server-side validation
 * after client-side checks pass.
 */
import { fromStore } from "svelte/store";
import type { Readable } from "svelte/store";
import type { FieldStatus } from "$lib/shared/forms/field-status.js";
import * as m from "$lib/paraglide/messages.js";

/**
 * Minimal form shape — uses `any` for store properties to avoid
 * SuperForm generic invariance issues. Internally cast via fromStore().
 */
interface FormLike {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  form: any;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  errors: any;
}

export interface AsyncCheckResult {
  valid: boolean;
  errorMessage?: string;
}

export interface UseAsyncFieldCheckOptions {
  form: FormLike;
  /** Form field name to validate. */
  field: string;
  /** Async checker — called with the field value. */
  checker: (value: string) => Promise<AsyncCheckResult>;
  /** Optional client-side gate. Skips check if returns false. @default value is truthy */
  preCheck?: (value: string) => boolean;
}

export interface UseAsyncFieldCheckReturn {
  /** Current validation status. */
  readonly status: FieldStatus;
  /** Call on blur to trigger async validation. */
  check: () => Promise<void>;
  /** Call on input to reset status. */
  reset: () => void;
}

export function useAsyncFieldCheck(options: UseAsyncFieldCheckOptions): UseAsyncFieldCheckReturn {
  const { form, field, checker, preCheck } = options;

  const formState = fromStore(form.form as Readable<Record<string, string>>);
  const errorsState = fromStore(form.errors as Readable<Record<string, string[]>>);

  let status = $state<FieldStatus>("idle");

  async function check() {
    const value = formState.current[field];

    // Client-side gate
    if (preCheck ? !preCheck(value) : !value) return;

    // Skip if client validation already failed
    const clientErrors = errorsState.current?.[field];
    if (clientErrors?.length) return;

    status = "validating";
    try {
      const result = await checker(value);
      if (!result.valid) {
        status = "invalid";
        form.errors.update((e: Record<string, string[]>) => ({
          ...e,
          [field]: [result.errorMessage ?? m.webclient_forms_validation_failed()],
        }));
      } else {
        status = "valid";
      }
    } catch {
      status = "idle";
    }
  }

  function reset() {
    status = "idle";
  }

  return {
    get status() {
      return status;
    },
    check,
    reset,
  };
}
