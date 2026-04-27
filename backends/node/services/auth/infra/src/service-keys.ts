// Re-export all service keys from auth-app.
// Keys live alongside interfaces (in auth-app), but infra re-exports
// them for convenience so the composition root can import from either package.
export {
  ICreateSignInEventKey,
  IFindSignInEventsByUserIdKey,
  ICountSignInEventsByUserIdKey,
  IGetLatestSignInEventDateKey,
  IUpdateSignInEventWhoIsIdKey,
  IUpdateSessionWhoIsIdKey,
  IGetActiveSessionsByUserIdKey,
  IGetUserIdByIdentifierKey,
  ICreateEmulationConsentRecordKey,
  IFindEmulationConsentByIdKey,
  IFindActiveConsentsByUserIdKey,
  IFindActiveConsentByUserIdAndOrgKey,
  IRevokeEmulationConsentRecordKey,
  ICreateOrgContactRecordKey,
  IFindOrgContactByIdKey,
  IFindOrgContactsByOrgIdKey,
  IUpdateOrgContactRecordKey,
  IDeleteOrgContactRecordKey,
  ICheckOrgExistsKey,
  ISignInThrottleStoreKey,
} from "@d2/auth-app";
