<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Utilities — Configuration

> Part of [`DcsvIo.D2.Utilities`](../README.md).

Host-side configuration plumbing — env-var ↔ wire-format conversion and `.env*` file loading. Used by every service composition root.

| File                        | Contents                                                                                                                                                   |
| --------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ConnectionStringHelper.cs` | URI ↔ wire-format converters for Redis / PostgreSQL / RabbitMQ env vars.                                                                                   |
| `D2Env.cs`                  | `.env*` file loader for host-side scenarios (tests, IDE debug, ad-hoc `dotnet run`). No-op inside Docker Compose (Compose handles env injection natively). |

## `ConnectionStringHelper` — URI ↔ wire-format conversion

Bridges standard URI-format env vars (`REDIS_URL=redis://:p@host:6379`, `*_DATABASE_URL=postgresql://u:p@host/db`, `RABBITMQ_URL=amqp://...`) into the wire formats expected by .NET clients (`StackExchange.Redis`, `Npgsql`). RabbitMQ accepts AMQP URIs natively — the RMQ helper just reads the env var.

```csharp
// Inside Geo.API/Program.cs:
var redis    = ConnectionStringHelper.GetRedis();
var postgres = ConnectionStringHelper.GetPostgres("GEO_DATABASE_URL");
var rabbit   = ConnectionStringHelper.GetRabbitMq();

// Or parse a known string directly (no env var read):
ConnectionStringHelper.ParseRedisUri("redis://:secret@host:6380");
// "host:6380,password=secret"

ConnectionStringHelper.ParsePostgresUri("postgresql://u:p@h:5433/db");
// "Host=h;Port=5433;Username=u;Password=p;Database=db"
```

Pass-through for already-converted values: `ParseRedisUri("h:6380,password=x")` → unchanged, `ParsePostgresUri("Host=h;Port=5432;Database=db")` → unchanged. Defaults: Redis port `6379`, PostgreSQL port `5432`. URL-encoded credentials (`%40` → `@`, `%3A` → `:`) are unescaped automatically.

`Get*` overloads throw `InvalidOperationException` with a "Check your `.env.local` file." hint when the env var is missing or empty — never silently fall back to localhost.

## `D2Env` — `.env*` file loader

For **host-side** scenarios (running tests, IDE debug, ad-hoc `dotnet run`) where Docker Compose's native `env_file:` injection is not in play. Inside Compose containers, env vars are already set by Compose before `Load()` runs — D2Env's "process-env wins" rule means this is a no-op.

```csharp
// Default — load .env, .env.local, .env.secrets in that order from the
// nearest discovery directory:
D2Env.Load();

// Explicit override — load only what you want, in the order you want:
D2Env.Load(".env.test", ".env.local");
```

### Discovery — "first directory wins"

Walks up from `AppContext.BaseDirectory` (max 12 levels) looking for the FIRST directory that contains AT LEAST ONE of the named files, then loads every matching file from THAT directory only. Files from different ancestor directories are NEVER mixed — prevents weird hybrid situations where the "wrong" `.env.secrets` from a higher directory gets paired with the "right" `.env.local` from a lower one.

```
C:\repo\.env.secrets         ← FOUND first
C:\repo\subproj\.env.local   ← FOUND first (different walk start)

If walk starts in C:\repo\subproj\bin\Debug:
  → discovery directory = C:\repo\subproj
  → loads C:\repo\subproj\.env.local only
  → does NOT pick up C:\repo\.env.secrets
```

### Precedence rules

1. **Process env wins over every file.** Any environment variable set when `Load()` was invoked (containers, IDE-injected vars, parent shell) is preserved unchanged. Files cannot overwrite container/parent values.
2. **Within file loading, later files in the list override earlier ones.** With the default `[".env", ".env.local", ".env.secrets"]`, `.env.secrets` overrides `.env.local`, which overrides `.env`. This matches Docker Compose's `--env-file` ordering, so host-side and container-side behavior match for the same .env files.

### Idempotency

`Load()` is safe (and cheap) to call multiple times. Subsequent calls are no-ops — the file system is not re-walked. Tests can re-trigger via the internal `ResetForTests()` seam.

### Caveat — case sensitivity

Env-var key collision detection uses the platform comparer: case-INsensitive on Windows (`PATH` and `path` are the same key), case-SENSITIVE everywhere else. D2's convention is uppercase env-var names; this matters only at the rare cross-OS edge.
