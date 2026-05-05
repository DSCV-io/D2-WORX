<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.AuthContext.Abstractions

> Parent: [`server/shared/dotnet/`](../README.md)

Read-only `IAuthContext` interface — the typed contract domain code uses to reason about caller identity, organization, scopes, and impersonation context. Codegen-emitted from `contracts/auth-context/IAuthContext.spec.json` by `D2.Shared.Context.SourceGen`. Plus hand-written `IAuthContextExtensions` convenience helpers.

This is the domain-safe slice. Anything heavier (HTTP middleware, JWT validation, runtime population) lives in sibling libs: `D2.Shared.RequestContext.Abstractions` (transport-level fields), `D2.Shared.RequestContext` (mutable concrete + factories), `D2.Shared.Auth` (runtime JWT validation + KeyringClient).

---

## File layout

| Path | Contents |
|---|---|
| `D2.Shared.AuthContext.Abstractions.csproj` | csproj — `<EmitCompilerGeneratedFiles>` for visibility; analyzer ref to `context-source-gen`; `<AdditionalFiles>` for both context specs |
| `(generated) IAuthContext.g.cs` | Generated interface (lives in `obj/Generated/`) |
| `IAuthContextExtensions.cs` | Hand-written convenience helpers — `HasScope`, `HasAnyScope`, `HasAllScopes`, `IsStaff`, `IsAdmin`, `IsForcedImpersonation`, `IsConsentImpersonation`, `IsImpersonatorStaff`, `IsImpersonatorAdmin` |

---

## Spec → emitted shape

The spec at `contracts/auth-context/IAuthContext.spec.json` declares 5 sections:
- **Token + Trust**: `IsAuthenticated` (trinary), `Audience` (`IReadOnlyList<string>` per RFC 7519 §4.1.3), `SessionId`, `TokenIssuedAt`, `TokenExpiresAt`, `ActorChain` (RFC 8693 flattened outermost-first)
- **Identity**: `Subject` (raw `sub`), `UserId` (`sub` parsed as Guid), `Username`, `RequestedByClientId` (RFC 8693 §4.3 / RFC 9068 — client that requested THIS token), `ImmediateCallerClientId` (derived — outermost Service in chain), `OriginatingClientId` (derived — most-deeply-nested Service in chain, fallback to Subject for pure service-identity tokens), `IsServiceIdentity` (derived)
- **Organization**: `OrgId`, `OrgName`, `OrgType`, `OrgRole`
- **Impersonation**: `IsImpersonating` (derived), `ImpersonationKind` (derived), `ImpersonatedBy` (derived), `ImpersonationSessionId` (derived), `ImpersonatorOrgId` / `ImpersonatorOrgName` / `ImpersonatorOrgType` / `ImpersonatorOrgRole` (derived)
- **Scopes**: `Scopes`

All D²-custom claim-mapped properties use `d2_`-prefixed claim names. Standard OAuth/OIDC claims (`sub`, `aud`, `iat`, `exp`, `client_id`, `scope`, `act`) keep their canonical names.

### The five identity properties — when to use which

| Property | Source | When meaningful |
|---|---|---|
| `Subject` | `sub` claim (raw) | Always (when authenticated). For user tokens: a Guid string. For service-identity tokens: the OAuth client_id of the calling service. |
| `UserId` | `sub` claim parsed as Guid | When the token represents a user. Null for pure service-identity tokens. |
| `RequestedByClientId` | `client_id` claim (RFC 8693 §4.3) | The client that requested THIS specific token from the AS. Updates on every token exchange — for a multi-hop chain this is the client that triggered the most recent exchange, NOT the originating client. |
| `ImmediateCallerClientId` | Outermost Service entry in `ActorChain` | The service that immediately called this handler. Null when the user is calling directly with no service intermediary. |
| `OriginatingClientId` | Most-deeply-nested Service entry in `ActorChain`, fallback to `Subject` for pure service tokens | **The primary audit identifier** for end-to-end traceability across multi-hop sync + async chains. The first service that started this call chain. |

---

## Extension methods (hand-written)

```csharp
auth.HasScope("auth.password.change");
auth.HasAnyScope(Scopes.Self.Read, Scopes.Self.Write);
auth.HasAllScopes(Scopes.Auth.User.Impersonate.Consent, Scopes.Auth.Password.Change);

auth.IsStaff();                  // OrgType is Admin or Support
auth.IsAdmin();                  // OrgType is Admin

auth.IsForcedImpersonation();    // ImpersonationKind == Force
auth.IsConsentImpersonation();   // ImpersonationKind == Consent

auth.IsImpersonatorStaff();      // ImpersonatorOrgType is Admin or Support
auth.IsImpersonatorAdmin();      // ImpersonatorOrgType is Admin
```

---

## Dependencies

Project references:
- `D2.Shared.Auth.Abstractions` — `OrgType`, `Role`, `ActorKind`, `ImpersonationKind`, `ActorEntry`

Analyzer-only:
- `D2.Shared.Context.SourceGen` — emits `IAuthContext.g.cs`

---

## Reference

- [`contracts/auth-context/IAuthContext.spec.json`](../../../../contracts/auth-context/IAuthContext.spec.json) — source of truth for the interface shape
- [`D2.Shared.Context.SourceGen`](../context-source-gen/) — the generator
- [`D2.Shared.Auth.Abstractions`](../auth-abstractions/) — vocabulary types referenced by the interface
