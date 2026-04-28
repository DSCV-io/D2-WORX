/**
 * Represents the type of organization.
 *
 * Wire format is lowercase ("admin", "support", ...) matching the value
 * stored in the `organization.org_type` Postgres column AND the
 * `OrgTypeValues` constants in D2.Shared.Handler on .NET. Equality across
 * DB rows, JWT claims, and inter-service messages is therefore string-based
 * — no per-boundary case translation needed.
 *
 * The PascalCase TypeScript identifier is convenient for `OrgType.Admin`-style
 * call sites; the underlying string value is what crosses every wire.
 */
export enum OrgType {
  Admin = "admin",
  Support = "support",
  Affiliate = "affiliate",
  Customer = "customer",
  ThirdParty = "third_party",
}
