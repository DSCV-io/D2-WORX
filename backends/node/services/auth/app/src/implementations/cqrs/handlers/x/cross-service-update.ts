/**
 * SAGA helper for cross-service updates that must keep Geo and Auth in sync.
 *
 * **Order of operations:**
 * 1. Geo first (cross-service, riskier — fail-fast there before touching auth state)
 * 2. Auth second (same DB, near-zero failure risk but compensate if it does fail)
 * 3. If auth fails: roll Geo back to the pre-update contact via another updateContactsByExtKeys call
 * 4. If rollback ALSO fails: log at logger.fatal() (CRITICAL severity) so it surfaces in alerting
 *
 * Used by all handlers that mutate Geo + Auth together: UpdateUserLocale,
 * UpdateUserTimezone, UpdateUserRealName, UpdateOrgContact, VerifyEmailChange,
 * VerifyPhoneChange, RemovePhone.
 */

import type { ContactToCreateDTO } from "@d2/protos";
import { D2Result } from "@d2/result";
import type { IHandlerContext } from "@d2/handler";
import { TK } from "@d2/i18n";
import type { Complex } from "@d2/geo-client";

export interface CrossServiceUpdateParams<TAuthOutput> {
  /**
   * Snapshot of the contact BEFORE the update — used as the rollback target if
   * the auth step fails. Callers should fetch this via `getContactsByExtKeys`
   * at the top of the handler (we already need it for the spread/merge anyway).
   */
  readonly oldContact: ContactToCreateDTO;
  /** Target contact state to write to Geo. */
  readonly newContact: ContactToCreateDTO;
  /** Geo update handler (provided by caller from DI). */
  readonly updateContactsByExtKeys: Complex.IUpdateContactsByExtKeysHandler;
  /**
   * The auth-side update. Returning a non-success D2Result triggers the Geo
   * rollback. Throwing also triggers rollback (treated as auth failure).
   */
  readonly authUpdate: () => Promise<D2Result<TAuthOutput | undefined>>;
  /** For logging — short label, e.g. "user.locale", "user.email", "user.phone". */
  readonly operationLabel: string;
  readonly context: IHandlerContext;
  /**
   * Optional callback invoked after a successful Geo update — lets callers
   * capture the Geo response (e.g. the new ContactDTO) for inclusion in their
   * own output. Called BEFORE the auth update; if auth fails and Geo is rolled
   * back, the captured value should NOT be used.
   */
  readonly onGeoSuccess?: (
    result: D2Result<Complex.UpdateContactsByExtKeysOutput | undefined>,
  ) => void;
}

/**
 * Runs Geo + Auth updates with compensating rollback.
 *
 * Returns the auth update's result (D2Result) on success. On Geo failure or
 * (auth failure → Geo rollback), returns a serviceUnavailable D2Result.
 */
export async function runCrossServiceUpdate<TAuthOutput>(
  params: CrossServiceUpdateParams<TAuthOutput>,
): Promise<D2Result<TAuthOutput | undefined>> {
  const {
    oldContact,
    newContact,
    updateContactsByExtKeys,
    authUpdate,
    operationLabel,
    context,
    onGeoSuccess,
  } = params;

  // 1. Geo first
  const geoResult = await updateContactsByExtKeys.handleAsync({ contacts: [newContact] });
  if (!geoResult.success) {
    context.logger.error(`${operationLabel}: Geo update failed, aborting`, {
      errorCode: geoResult.errorCode,
      statusCode: geoResult.statusCode,
    });
    return D2Result.serviceUnavailable({
      messages: [TK.common.errors.SERVICE_UNAVAILABLE],
    });
  }
  onGeoSuccess?.(geoResult);

  // 2. Auth second
  let authResult: D2Result<TAuthOutput | undefined>;
  try {
    authResult = await authUpdate();
  } catch (err) {
    // Throw treated as auth failure — fall through to compensation.
    context.logger.error(`${operationLabel}: auth update threw, will compensate`, {
      error: err instanceof Error ? err.message : String(err),
    });
    authResult = D2Result.serviceUnavailable({
      messages: [TK.common.errors.SERVICE_UNAVAILABLE],
    });
  }

  if (authResult.success) {
    return authResult;
  }

  // 3. COMPENSATE: roll Geo back to the pre-update contact.
  const compensateResult = await updateContactsByExtKeys.handleAsync({
    contacts: [oldContact],
  });
  if (!compensateResult.success) {
    // CRITICAL severity — system state is inconsistent across services.
    // Surfaces in alerting / on-call dashboards. Manual reconciliation required.
    context.logger.fatal(
      `${operationLabel}: CRITICAL cross-service inconsistency — auth update failed AND Geo rollback failed. Manual reconciliation required.`,
      {
        authErrorCode: authResult.errorCode,
        authStatusCode: authResult.statusCode,
        compensateErrorCode: compensateResult.errorCode,
        compensateStatusCode: compensateResult.statusCode,
      },
    );
  } else {
    context.logger.warn(`${operationLabel}: auth failed, Geo rolled back successfully`, {
      authErrorCode: authResult.errorCode,
    });
  }

  // Bubble the auth failure back to the caller.
  return authResult;
}
