<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.RequestContext

> Parent: [`server/shared/dotnet/`](../README.md)

Mutable concrete + cross-transport envelope record + hand-written claim parsers + the `MalformedActorChainException` type. The mutable concrete (`MutableRequestContext`) implements `IRequestContext`; only middleware writes to it; downstream domain code only sees the read-only interface.

The mutable class + envelope are codegen-emitted from `contracts/{auth,request}-context/*.spec.json` by `D2.Shared.Context.SourceGen`. The two structured-claim parsers + the malformed-chain exception are hand-written.

---

## File layout

| Path | Contents |
|---|---|
| `D2.Shared.RequestContext.csproj` | csproj — refs to abstractions + utilities + analyzer ref to `context-source-gen` + AdditionalFiles for both context specs |
| `(generated) MutableRequestContext.g.cs` | Generated mutable concrete (lives in `obj/Generated/`); 3 factory methods (`FromContextEnvelope`, `FromJwtPayloadNoValidation`, `FromClaims`); derived getters that walk the actor chain |
| `(generated) ContextEnvelope.g.cs` | Generated sealed record carrying every non-derived field — used as the encrypted-payload propagation shape across AMQP per `docs/MESSAGING.md` |
| `ActorChainParser.cs` | Hand-written RFC 8693 §2.1 actor-chain parser (recursive `act` → flat list, outermost first). Strict-mode: throws `MalformedActorChainException` on missing required claims, depth overflow, or invalid JSON |
| `ScopeClaimParser.cs` | Hand-written RFC 6749 §3.3 scope-claim parser (space-separated string per RFC OR JSON array defensively) |
| `MalformedActorChainException.cs` | Exception thrown by `ActorChainParser` for structurally-invalid `act` claims. Auth middleware MUST catch and convert to 401 |

---

## Three factory methods

```csharp
// HTTP / gRPC.AspNetCore — typical authn middleware path. Trusts that the principal
// has already been validated by the AspNetCore authentication middleware.
var ctx = MutableRequestContext.FromClaims(httpContext.User);

// ⚠ DOES NOT VALIDATE THE JWT. Caller MUST set IsAuthenticated = true after
// confirming signature, expiry, audience, and issuer. The factory deliberately
// leaves IsAuthenticated = false so unintended use surfaces as broken auth in dev.
using var payloadDoc = JsonDocument.Parse(jwtPayloadJson);
var ctx = MutableRequestContext.FromJwtPayloadNoValidation(payloadDoc.RootElement);
ctx.IsAuthenticated = true;  // ← only after JwtValidator confirms the token

// RabbitMQ consumer — restore from the encrypted envelope on the message
var envelope = JsonSerializer.Deserialize<ContextEnvelope>(decryptedPayload);
var ctx = MutableRequestContext.FromContextEnvelope(envelope);
```

All three populate auth fields (claims-derived). Transport-level fields (TraceId / ClientIp / fingerprints / WhoIs) require their respective filling extensions (handler-aspnetcore for HTTP, geo-client for WhoIs lookups, handler-messaging for AMQP consumers).

---

## Strict-mode actor-chain parsing

`ActorChainParser` rejects malformed actor chains by throwing `MalformedActorChainException`:

| Condition | Reason |
|---|---|
| Any entry missing `sub` | RFC 8693 §2.1 violation ("Each act entry MUST contain a sub claim") |
| Impersonation entry missing `d2_kind` / `d2_session_id` / `d2_org_id` / `d2_org_type` / `d2_org_role` | D² impersonation contract — these fields are required for audit and session tracking |
| Depth exceeds `MaxActDepth` (20) | DoS protection — real chains are 1–4 hops; >10 is suspicious; 20 is the hard wall |
| Invalid JSON or non-object root | Token is structurally invalid |

**Auth middleware MUST catch and convert to `D2Result.Unauthorized` (HTTP 401).** Letting the exception bubble through `BaseHandler.RunCorePipelineAsync` would surface as 500 UnhandledException — wrong signal for a malformed token.

---

## ContextEnvelope is identity claims, NOT a token

The `ContextEnvelope` carries identity CLAIMS (Subject, UserId, OrgId, Scopes, ActorChain, fingerprints, etc.) — not a live JWT. Envelopes don't expire — they're snapshots of who-is-acting and where-from at the moment of publication. This is the Netflix-Passport pattern adapted for D²'s threat model (encrypted payload, not signed envelope, since AMQP brokers stay blind).

Async consumers needing to make sync calls request fresh tokens via the auth runtime's exchange endpoint, presenting their own service identity + the envelope's identity claims. Long-running workflows (image processing, sagas) work because the envelope doesn't time out the way a token would.

---

## Upload-context envelope storage (S3 + similar async event sources)

When a request triggers an out-of-band upload (presigned S3 URL, multipart upload, etc.) followed by an asynchronous "object created" event, the original request context needs to bridge across the gap. The upload itself doesn't carry a JWT (presigned URLs use AWS-side signing) and the S3 event doesn't carry the user's identity.

### Storage design

**Embed the file_id in the S3 key path** so the bridge consumer can extract it from the event:
```
s3://<bucket>/uploads/<org_id>/<file_id>/<original_filename>
```

**At upload-begin** (sync handler with full context):
1. Generate `file_id` (UUIDv7 for time-orderability).
2. Construct the S3 key with file_id embedded.
3. Generate the presigned S3 URL (15-min TTL).
4. Build a `ContextEnvelope` from the current `IRequestContext`.
5. Encrypt + store in Redis at key `upload-context:{file_id}` with TTL **60 minutes** (covers upload window + S3 event delivery + bridge consumer lag + grace for retries).
6. Insert a row in the files table with status `pending_upload`.
7. Return `(file_id, presignedUrl)` to the client.

**At S3 event ingress** (bridge consumer):
1. Extract `file_id` from the S3 object key (regex on path).
2. Fetch envelope from Redis at `upload-context:{file_id}`.
3. Decrypt → publish to internal AMQP with the envelope embedded in the encrypted message payload.
4. Do NOT delete the Redis entry on consumption — S3 events can be at-least-once; let TTL handle cleanup.

**Fallback (envelope expired)**:
- Look up the file row by file_id from the DB.
- Synthesize a degraded envelope (Subject + OrgId from the file row; everything else null).
- Set `IsSyntheticEnvelope = true` so downstream consumers can decide whether to proceed under degraded provenance.
- Log a warning so we catch upload-context-expired patterns.

### Why 60 min TTL

| Phase | Duration |
|---|---|
| Presigned URL TTL | 15 min |
| S3 event delivery | seconds–~1 min under load |
| Bridge consumer lag | seconds–minutes during deploy/restart |
| Grace for retries on bridge crash | minutes |
| Total worst case | ~30 min |

60 min is 2× the worst-case for safety margin. After 60 min, abandoned uploads silently expire; pathological delays fall back to the synthetic envelope path.

### IsSyntheticEnvelope handling

`IRequestContext.IsSyntheticEnvelope` (default `false`) flags reconstructed-from-DB context. Handlers handling sensitive operations should consider rejecting (`D2Result.ServiceUnavailable("Original request context lost")`) rather than processing under degraded provenance — the FingerprintMatchScore, ImmediateCallerClientId, scopes-at-the-time-of-original-request, etc. are all unavailable for synthetic envelopes. Routine operations (delivering a notification, updating a profile pic URL) can proceed with the degraded context.

---

## Dependencies

Project references:
- `D2.Shared.RequestContext.Abstractions` — interface
- `D2.Shared.AuthContext.Abstractions` — base interface
- `D2.Shared.Auth.Abstractions` — vocabulary types
- `D2.Shared.Utilities` — string/Guid/Enum extensions used by parsers + emitted code

Analyzer-only:
- `D2.Shared.Context.SourceGen` — emits the mutable concrete + envelope

BCL only — no extra packages. `ClaimsPrincipal` lives in `System.Security.Claims`; `JsonElement` lives in `System.Text.Json`.

---

## Reference

- [`D2.Shared.RequestContext.Abstractions`](../request-context-abstractions/) — interface
- [`D2.Shared.Context.SourceGen`](../context-source-gen/) — generator
- [`docs/MESSAGING.md`](../../../../docs/MESSAGING.md) — encrypted-`ContextEnvelope` AMQP propagation
- [RFC 8693 §2.1](https://datatracker.ietf.org/doc/html/rfc8693#section-2.1) — actor chain
- [RFC 8693 §4.1](https://datatracker.ietf.org/doc/html/rfc8693#section-4.1) — actor-chain ordering semantics (outermost = current actor; deepest = originator)
- [RFC 6749 §3.3](https://datatracker.ietf.org/doc/html/rfc6749#section-3.3) — `scope` claim format
- [RFC 7519 §4.1.3](https://datatracker.ietf.org/doc/html/rfc7519#section-4.1.3) — `aud` claim shape (string OR array)
