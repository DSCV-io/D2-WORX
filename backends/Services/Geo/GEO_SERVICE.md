# Geo Microservice - Architecture & Design

## Overview

The **Geo** (Geography) microservice is a critical infrastructure service within the D2 ecosystem responsible for managing geographic, location, and contact information. It serves as a system of record for:

- **Locations** - Physical addresses with coordinates and geopolitical context
- **Contacts** - Contact information for individuals and organizations
- **WHOIS** - IP address geolocation and network metadata with device fingerprinting
- **Reference Data** - Countries, subdivisions, currencies, languages, locales
- **Geopolitical Entities** - Supra-national organizations (NATO, EU, etc.)

---

## Core Architectural Principles

### 1. Immutability by Design

All data in Geo is **immutable**. Updates create new records rather than modifying existing ones.

**Rationale:**
- **Thread-safe** - No locks needed for concurrent reads
- **Cacheable** - Cached data never becomes stale, only superseded
- **Audit trail** - Complete history preserved automatically
- **Simplified reasoning** - No hidden state changes

**Implementation:**
- Records use `init` accessors only (no `set`)
- Updates create new entities with new IDs
- Old records remain for historical reference
- Consuming services update references to new IDs

### 2. Content-Addressable Storage for Deduplication

Location and WHOIS entities use **SHA-256 hash as primary key** instead of sequential IDs.

**Rationale:**
- **Automatic deduplication** - Same content = same hash = same record
- **Deterministic lookups** - No need to search before creating
- **Natural partitioning** - Hash-based sharding for distributed systems
- **Efficient retrieval** - Direct lookup without full-text search

**Hash Input (Location):**
Normalized string from: coordinates, street address, city, postal code, subdivision code, country code

**Hash Input (WHOIS):**
Normalized string from: IP address + device fingerprint (composite key differentiates devices on same IP)

### 3. Loose Coupling via Context Keys

Contacts use a **ContextKey + RelatedEntityId** pattern instead of foreign keys.

**Rationale:**
- **Service independence** - No database-level coupling to other services
- **Flexible caching** - Bulk load by context ("give me all user contacts")
- **Domain flexibility** - Same Contact can serve multiple domains
- **Migration friendly** - Services can change without Geo schema changes

**ContextKey Examples:**
- `"auth-user"` - Contact belongs to a user in Auth service
- `"sales-order"` - Contact belongs to an order in Sales service
- `"invoice"` - Contact belongs to an invoice in Billing service

**RelatedEntityId:**
References the external entity's ID (User.Id, Order.Id, etc.) without FK constraint

---

## Domain Model Structure

### Aggregate Roots

**Aggregate roots are the primary entry points for domain operations:**

#### Contact
Individual or organizational contact information with optional location reference.

**Key Properties:**
- Version 7 GUID for time-ordered IDs
- ContextKey for categorization/caching
- RelatedEntityId for loose coupling to external entities
- Optional nested value objects: ContactMethods, Personal, Professional
- Optional FK to Location (byte[] hash)

**Design Decisions:**
- ContactMethods, Personal, Professional are nullable (contacts vary widely)
- CreatedAt always UTC (display timezone conversion in UI)
- LocationHashId is byte[] to match Location primary key type

#### Location
Physical location with content-addressable hash ID.

**Key Properties:**
- SHA-256 hash (byte[]) as primary key
- Optional coordinates, street address, city, postal code
- References to subdivision and country (ISO codes)

**Design Decisions:**
- All address fields optional (flexibility for incomplete data)
- Coordinates quantized to 5 decimal places (~1.1m precision)
- Hash includes normalized lowercase strings for consistency

#### WHOIS
IP address geolocation and network metadata with device fingerprinting.

**Key Properties:**
- SHA-256 hash (byte[]) as primary key (computed from IP + fingerprint)
- IP address (string)
- Device fingerprint (string) - differentiates devices sharing same IP
- ASN information (number, name, country)
- Geographic information (city, country, coordinates)
- LastUpdated (DateOnly) for staleness detection

**Design Decisions:**
- Content-addressable by IP + fingerprint (same IP, different device = different record)
- Fingerprint critical for home/corporate networks (multiple devices, one public IP)
- All geo fields optional (external API may not return all data)
- DateOnly for LastUpdated (no time component needed for daily refresh checks)
- Immutable - updates create new records with new hash

**Use Cases:**
- Track user sessions by geographic location and device
- Detect suspicious login attempts from unusual locations
- Enrich analytics with geographic data
- Rate limit by network (ASN) rather than just IP

#### Country
Sovereign state with ISO codes and metadata.

**Key Properties:**
- ISO 3166-1 alpha-2 code (primary key)
- ISO 3166-1 alpha-3 code, numeric code
- Official name, common name
- Collection of Subdivision child entities

**Design Decisions:**
- ISO code as PK (stable, standardized)
- Subdivisions are owned child entities (part of Country aggregate)

#### GeopoliticalEntity
Supra-national organizations and agreements.

**Key Properties:**
- Unique code (NATO, EU, USMCA)
- Name, abbreviation, type (enum)
- Member countries (list of ISO codes)
- Established/dissolved dates

**Design Decisions:**
- Separate aggregate from Country (different lifecycle)
- Member countries stored as list (join table if complex queries needed)
- Type enum: MilitaryAlliance, TradeAgreement, EconomicUnion, etc.

### Child Entities

#### Subdivision
Administrative division within a country (state, province, region).

**Relationship:**
- Child entity of Country aggregate
- Accessed through Country, not directly

**Key Properties:**
- ISO 3166-2 code
- Name, type (state, province, canton, etc.)
- Parent country reference

### Reference Data Entities

**These are standalone entities (not aggregates) that serve as lookup/reference data:**

#### Currency
Monetary currency information.

**Key Properties:**
- ISO 4217 code (USD, EUR, JPY)
- Symbol, name, decimal places

**Usage:**
Referenced by CountryCurrency entities

#### Language
Human language information.

**Key Properties:**
- ISO 639-1 code (2-letter: en, fr, es)
- ISO 639-3 code (3-letter: eng, fra, spa)
- Name, native name

**Usage:**
Referenced by Locale entities

#### Locale
Language + region combination.

**Key Properties:**
- Language reference (FK)
- Region code (ISO 3166-1 alpha-2)
- Computed locale code (en-US, fr-CA)

**Usage:**
Represents regional language variants

#### CountryCurrency
Join entity linking countries to currencies.

**Key Properties:**
- Country reference (FK)
- Currency reference (FK)
- IsPrimary flag

**Rationale:**
- Many-to-many relationship needed
- Many countries use multiple currencies (tourism, trade)
- Primary flag indicates official/main currency

**Examples:**
- US -> USD (primary)
- Croatia -> EUR (primary), USD (tourism)
- Panama -> USD (primary), PAB (secondary)

---

## Value Objects

Value objects are immutable, have no identity, and are defined by their properties.

### ContactMethods
Collection of emails and phone numbers with validation.

**Design Decisions:**
- Required collections (use empty list, not null)
- Validates all nested EmailAddress/PhoneNumber on creation
- First in list = primary by convention
- Provides PrimaryEmail/PrimaryPhone convenience properties

### EmailAddress
Validated email with user-defined labels.

**Design Decisions:**
- Value cleaned (trimmed, lowercased)
- Basic regex validation (domain enforces format, not RFC 5322 compliance)
- Labels as ImmutableHashSet (unordered, no duplicates)
- Labels optional/empty (not all emails need categorization)

### PhoneNumber
Validated phone number with user-defined labels.

**Design Decisions:**
- Store E.164 format: digits only, no symbols
- Strip all formatting on input (accept any format, normalize to digits)
- 7-15 digit validation (E.164 spec)
- No leading `+` stored (implied)
- Format for display in UI layer, not domain

**Rationale for digits-only storage:**
- Consistent storage format
- Smaller storage footprint
- Easy comparison/deduplication
- UI handles formatting per user locale

### Personal
Personal information about an individual.

**Design Decisions:**
- FirstName required, all else optional (minimal contact = name only)
- DateOnly for birth date (no time component)
- Professional credentials as list with property initializer (defaults to empty)
- Only BiologicalSex captured (Male, Female, Intersex, Unknown)
- Gender identity removed for simplicity (can add later if needed)

**Enum Choices:**
- NameTitle: Mr, Ms, Miss, Mrs, Mx, Dr, Prof, Rev, etc. (closed set)
- GenerationalSuffix: Jr, Sr, I-X (closed set)
- Professional credentials: string list (open set - too many to enumerate)

### Professional
Business/company information.

**Design Decisions:**
- CompanyName required
- Website as Uri type (built-in format validation)
- JobTitle, Department optional

### Coordinates
Geographic coordinates in decimal degrees.

**Design Decisions:**
- Decimal type for precision
- Range validation: latitude -90 to 90, longitude -180 to 180
- Quantized to 5 decimal places (~1.1m precision, sufficient for addresses)

### StreetAddress
Multi-line street address.

**Design Decisions:**
- Line1 required, Line2/Line3 optional
- Line3 cannot exist without Line2 (business rule validation)
- All lines cleaned (whitespace normalized)

---

## Data Type Decisions

### DateTime vs DateOnly vs DateTimeOffset

**DateOnly** - Calendar dates without time component:
- Birth dates
- Established/dissolved dates for geopolitical entities
- WHOIS change dates (day-granular from API)

**DateTime (always UTC)** - Audit timestamps:
- CreatedAt, UpdatedAt fields
- Always stored in UTC
- Converted to viewer's timezone in UI layer

**DateTimeOffset** - Not currently used:
- Would be for scheduled events where original timezone matters
- Not needed in current Geo domain

**Rationale:**
Don't store time when it's meaningless. Birth date "1990-05-15" is a calendar date regardless of timezone.

### String vs Enums

**Use Enums when:**
- Closed set of values with domain meaning
- Compiler enforcement valuable
- Examples: BiologicalSex, NameTitle, GenerationalSuffix

**Use Strings when:**
- Open set (user-defined values)
- Values vary by application context
- Too many values to enumerate
- Examples: ContextKey, email/phone labels, professional credentials

### Collection Types

**ImmutableList<T>** - Ordered collections where sequence matters:
- Emails (first = primary)
- Phone numbers (first = primary)

**ImmutableHashSet<T>** - Unordered collections, no duplicates:
- Labels for emails/phones (order doesn't matter)

**Property Initializers** - Default empty collections:
```
public ImmutableList<string> Tags { get; init; } = [];
```
Use for optional collections that are rarely populated.

**Required Collections** - No default, must be set:
```
public required ImmutableList<EmailAddress> Emails { get; init; }
```
Use for core collections that should be explicitly provided (even if empty).

---

## Multi-Tier Caching Strategy

Geo is **critical infrastructure** - most services depend on it. Multi-tier caching ensures resilience during outages.

### Tier 1: Redis Cache (Geo Service)

**Purpose:** Reduce database load, fast lookups

**Structure:**
- Partition by entity type and ContextKey
- Key patterns: `geo:contacts:user:*`, `geo:contacts:order:*`
- Location by hash: `geo:locations:{hashId}`

**Operations:**
- GetContactsByType() streams from Redis
- Individual lookups check Redis before database
- Write-through on creates

**Configuration:**
- Dedicated Redis instance (not shared with other services)
- Persistence enabled (RDB + AOF)
- Cluster mode for high availability

### Tier 2: Local Memory Cache (Consuming Services)

**Purpose:** Sub-millisecond lookups, survive Geo service latency/outages

**Pattern:**
Each consuming service maintains in-memory cache of relevant contacts:
- Order Service caches "order" context contacts
- Auth Service caches "user" context contacts
- Separate cache per ContextKey type

**Lifecycle:**
1. Warm cache on startup (bulk load from Geo)
2. Check local cache first for every request
3. Fall back to Geo service on cache miss
4. Update local cache on response

**Benefits:**
- No network latency
- Continues working during Geo service restarts
- Reduces load on Geo service

**Eviction:**
- LRU eviction for memory management
- TTL optional (immutability means stale data is just old version)
- Explicit invalidation unnecessary (updates = new IDs)

### Tier 3: Local Disk Cache (Consuming Services)

**Purpose:** Survive process restarts, disaster recovery

**Implementation:**
- Periodic snapshots of memory cache to disk (JSON files)
- Load from disk on startup if available
- Fall back to Geo service if disk cache missing/corrupted

**Storage:**
- Service-local directory: `/var/cache/{service-name}/geo/`
- Separate files per ContextKey: `contacts-user.json`, `contacts-order.json`

**Benefits:**
- Survives pod/container restarts
- Fallback if Geo and Redis both down
- Faster startup (local disk vs network)

**Refresh:**
- Load from disk on startup
- Async refresh from Geo service after startup
- Replace disk cache with fresh data

### Cache Invalidation

**Immutability = Simple Invalidation:**

Updates create new entities with new IDs. Old cached entries become obsolete naturally.

**Pattern:**
1. User updates email
2. Geo creates new Contact with new ID
3. Auth service updates User.ContactId to new ID
4. Old Contact ID in cache eventually evicted via LRU
5. No explicit invalidation needed

**Eventual Consistency:**
- Services may see old Contact briefly
- Acceptable for non-critical data (contact info)
- Critical updates can force cache refresh

---

## Validation Philosophy

### Domain Layer: Fail Fast

Domain objects enforce invariants strictly. Invalid state throws exceptions.

**Rationale:**
- Invalid data = programming error, not user error
- Domain protects business rules
- Early failure prevents corrupt state

**Pattern:**
- Create() methods validate inputs
- Throw domain exceptions (GeoValidationException)
- No "try" or "isValid" methods in domain

**Example Validations:**
- Email format check
- Phone number 7-15 digits
- Coordinates in valid range
- Required fields not null/empty

### Application Layer: Graceful Handling

Application layer catches domain exceptions and returns user-friendly errors.

**Pattern:**
- Try/catch around domain operations
- Convert exceptions to Result<T> or error DTOs
- Return appropriate HTTP/gRPC status codes

**Separation:**
- Domain: "This violates business rules" (throw)
- Application: "Here's what went wrong" (return error)

### Validation on Create vs Deserialize

**Two Create() Overloads:**
1. Create from raw values (validates and constructs)
2. Create from existing instance (re-validates after deserialization)

**Rationale:**
- Deserialized data might be corrupted/tampered
- Re-validation ensures invariants even after JSON roundtrip
- Consistent validation regardless of data source

---

## API Design Principles

### gRPC Services

**Advantages for Geo:**
- Efficient binary serialization
- Streaming support for bulk operations
- Strong typing via Protobuf
- Native support in .NET ecosystem

**Key Patterns:**
- Unary RPC for single entity lookups
- Server streaming for bulk operations (GetContactsByType)
- Streaming enables large result sets without memory issues

### Error Handling

**Domain Exceptions -> gRPC Status:**
- GeoValidationException -> StatusCode.InvalidArgument
- Entity not found -> StatusCode.NotFound
- Infrastructure errors -> StatusCode.Internal/Unavailable

**Error Details:**
Include structured error info:
- Object type, property name, invalid value
- Human-readable message
- Enables client-side error handling

### Bulk Operations for Caching

Specialized endpoints for cache warming:
- GetContactsByType() - stream all contacts of given ContextKey
- Optimized for startup cache loading
- Pagination via cursor pattern for very large sets

---

## Caching Architecture

### Three-Tier Caching Strategy

**Tier 1: PostgreSQL (Source of Truth)**
- All writes go here
- Rarely read directly (only on cache miss)
- Single authoritative source

**Tier 2: Redis (Primary Cache - Shared)**
- Holds all transactional data (Contacts, WHOIS, Locations)
- All services read directly from Redis
- Sub-millisecond lookups (0.1-1ms)
- Single shared instance across all services
- Populated by Geo service on miss
- Can handle millions of records easily (10GB+ = 10M+ records)

**Tier 3: Local Memory Cache (Per Service Instance)**
- LRU cache with TTL (1 hour - 1 day depending on data type)
- Only hot items (1K-10K records per service)
- Sub-microsecond lookups
- Automatic eviction via LRU
- No persistence needed

**Local Disk (Reference Data Only)**
- ONLY for small, static reference data:
  - countries.json (~250 records)
  - currencies.json (~180 records)
  - languages.json (~200 records)
  - subdivisions.json (~5K records)
- Total < 5MB
- Loaded once on service startup
- Fallback if both Redis and Geo unavailable
- **Never used for transactional data (Contacts, WHOIS, Locations)**

### Why This Architecture?

**Redis as Primary Cache (Not Full Local Replication):**
- ✅ Each service only accesses a small subset of total data
- ✅ Order service doesn't need Auth contacts
- ✅ Auth service doesn't need all 18M WHOIS records
- ✅ Redis can handle millions of records without issue
- ✅ Avoids disk space waste
- ✅ Eliminates sync complexity
- ✅ Fast service startup (no millions of records to load)
- ✅ Simple maintenance

**Local Memory LRU (Not Full Dataset):**
- ✅ Each service caches only what it actually uses
- ✅ Automatic eviction keeps memory bounded
- ✅ No manual cache management needed
- ✅ Works naturally with service access patterns

### Data Type Caching Strategies

#### Reference Data (Countries, Currencies, Languages)
**Storage:**
- Redis: unlimited TTL (effectively permanent)
- Local disk: JSON files as fallback
- Local memory: loaded on startup, never evicted

**Rationale:**
- Small datasets (< 5MB total)
- Changes rarely (months/years)
- Same across all services
- Critical for operation (need fallback)

**Read Path:**
```
1. Check local memory (loaded at startup)
2. If empty → load from disk JSON
3. If disk missing → fetch from Redis
4. If Redis missing → call Geo service
```

#### Transactional Data (Contacts, WHOIS, Locations)
**Storage:**
- Redis: TTL varies by type (1-30 days)
- Local memory: LRU with TTL (1 hour - 1 day)
- No disk storage (too large, changes frequently)

**Rationale:**
- Large datasets (millions of records)
- Changes frequently (daily/weekly)
- Different subset per service
- Not critical for operation (graceful degradation acceptable)

**Read Path:**
```
1. Check local memory LRU
2. If miss → check Redis
3. If Redis miss → call Geo to populate Redis
4. Cache locally with appropriate TTL
```

### WHOIS Lookup Flow

**Step-by-step process:**

1. Compute hash from IP address + fingerprint
2. Check local memory cache (hot items only)
   - If found → return immediately (sub-microsecond)
3. Check Redis directly
   - If found → cache locally with 24h TTL, return result (sub-millisecond)
4. Redis miss → call Geo service to create and populate Redis
   - If creation fails (invalid IP, API down) → log warning, return null
5. Read from Redis again (Geo just populated it)
   - If found → cache locally, return result
6. Still null → log error (race condition or Geo failure), return null

**Benefits:**
- Hot items cached locally (sub-microsecond)
- Cold items in Redis (sub-millisecond)
- Automatic eviction (LRU)
- No disk management
- Simple, clean flow

### Contact Lookup Flow

**Step-by-step process:**

1. Check local memory cache
   - If found → return immediately (sub-microsecond)
2. Check Redis
   - If found → cache locally with 1h TTL, return result (sub-millisecond)
3. Redis miss → contact doesn't exist or was evicted
   - Log warning, return null
   - Consuming service handles missing contact appropriately

**Note on contacts:**
- Unlike WHOIS (enrichment data), missing contacts may indicate an error condition
- Services should handle null returns gracefully (retry, error message, etc.)
- Contacts are typically created explicitly, not on-demand like WHOIS

### Cache Warming on Startup

**Reference Data (Required):**

1. Try loading from local disk JSON files first (fastest)
   - If successful → load into memory, done
2. Fallback to Redis if disk files missing/corrupt
   - Fetch all countries, currencies, languages from Redis
   - Load into memory
   - Save to disk for next startup

**Transactional Data (Optional):**

1. Determine which data this service actually needs
   - Use service-specific ContextKey for contacts
   - WHOIS typically not warmed (too large, access pattern is sparse)
2. Stream relevant data from Geo service
   - Populate Redis as data arrives
   - Populate local memory cache simultaneously
3. Continue to serve requests during warming (non-blocking)

**Strategy differences:**
- Reference data: blocking (must complete before serving requests)
- Transactional data: non-blocking (serve requests immediately, warm in background)

### Graceful Degradation

**When Redis is Down:**
1. Services continue with local memory cache (hot items still work)
2. Reference data falls back to disk JSON
3. New reads that aren't cached → return null, log warning
4. Critical operations can retry with backoff or degrade features
5. System remains partially operational

**When Geo Service is Down:**
1. Existing cached data continues to work (both Redis and local memory)
2. New data creation fails (expected behavior)
3. WHOIS enrichment disabled (acceptable - enrichment data, not critical)
4. Contact lookups still work if already cached
5. System operates in read-only mode for Geo data

**When PostgreSQL is Down:**
1. All reads work from cache (Redis + local memory)
2. Writes fail (expected behavior)
3. System operates fully read-only
4. Queue writes for retry when database recovers

**Philosophy:**
- WHOIS is enrichment → log warning, continue without it
- Contacts may be critical → return error, let consuming service decide
- Reference data is critical → must have disk fallback
- Graceful degradation > complete failure

### Cache Invalidation Strategy

**Immutability Eliminates Most Invalidation:**
- Updates create new records with new IDs
- Old cached records remain valid (historical data)
- Consuming services update to new IDs when ready
- No cache invalidation needed for updates

**TTL-Based Expiration:**
- Reference data: unlimited (effectively permanent)
- WHOIS: 30 days (IP assignments change slowly)
- Contacts: 1 day Redis, 1 hour local (moderate change rate)
- Locations: 7 days (addresses rarely change)

**Active Invalidation (Rare):**
- Only needed for corrections/deletes
- Publish invalidation event to message bus
- Services remove from local memory cache
- Redis removes on next scheduled cleanup
- PostgreSQL remains source of truth

### Redis Configuration

**Memory Limits:**
- Set maxmemory based on expected dataset size
- Use allkeys-lru eviction policy
- Monitor memory usage via metrics

**Persistence:**
- Enable AOF (Append-Only File) for durability
- Balance write performance vs data safety
- Snapshots every 1 hour as backup

**Connection Pooling:**
- Use StackExchange.Redis connection multiplexer
- Single connection per application
- Leverage Redis pipelining for bulk operations

### Metrics to Monitor

**Cache Performance:**
- Hit rate by tier (memory, Redis) and data type
- Miss rate by data type
- Average lookup latency by tier
- Cache size/item count by data type

**Degradation Indicators:**
- Redis connection failures
- Geo service call failures
- PostgreSQL connection failures
- Cache warming duration on startup

**Alerts:**
- Cache hit rate < 85% (indicates cache not warming properly)
- Redis connection pool exhaustion
- Startup cache warming > 30 seconds
- WHOIS creation failure rate > 5%

---

## Testing Strategy

### Unit Tests - Domain Logic

**Focus:** Value object and entity validation, business rules

**Examples:**
- Invalid coordinates throw exception
- Email cleaning/normalization works correctly
- Phone number format validation
- Nested object validation propagates

**Benefits:**
- Fast execution
- No infrastructure dependencies
- Test business logic in isolation

### Integration Tests - Data Layer

**Focus:** Persistence, retrieval, EF Core mappings

**Examples:**
- Save and retrieve entity preserves all properties
- JSONB columns serialize/deserialize correctly
- Foreign key relationships work
- Content-addressable hash lookups function

**Infrastructure:**
- Test database (PostgreSQL in Docker)
- Real EF Core context
- Test data setup/teardown

### End-to-End Tests - Caching

**Focus:** Multi-tier cache behavior, resilience, graceful degradation

**Examples:**
- Local memory cache serves hot items sub-microsecond
- Redis miss triggers Geo population correctly
- Disk cache fallback works for reference data
- Cache warming on startup completes successfully
- LRU eviction works as expected (memory bounded)
- Graceful degradation when Redis unavailable
- Graceful degradation when Geo service unavailable
- WHOIS fingerprint differentiation (same IP, different devices)

**Infrastructure:**
- Test Geo service (can simulate failures)
- Real Redis instance
- Real cache implementations (memory + disk)
- Fault injection (simulate downtime)
- Metrics collection for hit/miss rates

---

## Database Design Considerations

### PostgreSQL Features

**JSONB Columns:**
Used for ContactMethods collections (emails and phone numbers)

**Benefits:**
- Natural fit for one-to-many relationships (multiple emails/phones per contact)
- Flexible schema for labels and metadata
- GIN indexes for fast queries on nested properties
- Avoids separate join tables for simple collections

**What uses JSONB:**
- EmailAddresses collection (one JSONB column)
- PhoneNumbers collection (one JSONB column)

**What uses regular columns:**
- Personal information (flattened into Contact table columns)
- Professional information (flattened into Contact table columns)
- All other value objects that can be efficiently flattened

**Rationale:**
- JSONB only when one-to-many relationship benefits from it
- Flat columns when data can be efficiently normalized
- Keeps schema simple and queryable while maintaining flexibility where needed

**GIN Indexes:**
Enable fast JSONB queries on nested properties (e.g., search by email domain)

**Partitioning:**
- Partition Contacts by ContextKey for large datasets
- Improves query performance for context-specific lookups

### Migration Strategy

**Immutability Advantage:**
- Schema changes don't require data migration
- Old format coexists with new format
- Gradual migration as records are updated (new IDs created)

**Additive Changes Only:**
- Add new fields (nullable)
- Never remove or rename fields (breaks old data)
- Deprecate in code, not schema

---

## Deployment & Operations

### High Availability

**Database:**
- PostgreSQL with replication
- Read replicas for query load
- Automated failover

**Redis:**
- Redis Cluster for redundancy
- Multiple replicas
- Persistence enabled

**Service:**
- Multiple instances behind load balancer
- Health checks on database and Redis connectivity
- Circuit breaker for upstream dependencies

### Monitoring

**Key Metrics:**
- Cache hit rates (Redis, local memory)
- Database query latency
- gRPC call latency per operation
- Error rates by exception type

**Alerts:**
- Cache hit rate drops (indicates cache issues)
- Database connection pool exhaustion
- High error rates

### Disaster Recovery

**Backup Strategy:**
- PostgreSQL backups (daily full, continuous WAL)
- Redis snapshots (RDB)
- Test restore procedures regularly

**Failover:**
- Services can operate degraded with local caches
- Graceful degradation during Geo downtime
- Eventual consistency acceptable for non-critical data

---

## Future Considerations

### Geocoding Integration

Integrate external geocoding APIs for address validation and coordinate generation.

**Benefits:**
- Validate addresses against real-world data
- Auto-populate coordinates from address
- Suggest corrections for invalid addresses

**Implementation:**
- Application layer, not domain
- Optional enhancement, not core requirement

### Advanced Phone Validation

Integrate libphonenumber library for regional phone number validation.

**Benefits:**
- Country-specific validation
- Parse various formats correctly
- Detect mobile vs landline

**Implementation:**
- Application layer before calling domain
- Domain keeps simple validation (7-15 digits)

### Event Sourcing

Store all changes as events for complete audit trail.

**Benefits:**
- Full history of changes
- Point-in-time reconstruction
- Compliance/audit requirements

**Complexity:**
- Additional infrastructure (event store)
- More complex queries
- Consider only if compliance requires

### Search Capabilities

Add full-text search via Elasticsearch.

**Use Cases:**
- Search addresses by partial string
- Find contacts by name fragment
- Geospatial queries (contacts within radius)

**Implementation:**
- Elasticsearch indexes populated from database
- Eventual consistency acceptable
- Read path only, writes go through domain

---

## Summary

The Geo microservice provides critical geographic and contact infrastructure for the D2-Works ecosystem. Key architectural decisions:

**Immutability** - Simplifies caching, provides audit trail, enables concurrent access

**Content-addressable hashing** - Natural deduplication for Locations and WHOIS records

**WHOIS with fingerprinting** - Differentiates devices sharing same IP for accurate tracking

**Loose coupling** - ContextKey + RelatedEntityId avoids foreign key constraints

**Redis-primary caching** - Shared cache holds millions of records, services cache hot subset locally

**LRU memory caching** - Bounded memory per service, automatic eviction, sub-microsecond lookups

**Graceful degradation** - Services continue operating with reduced functionality during outages

**Reference data separation** - Small static data cached on disk, large transactional data in Redis only

**Validation boundaries** - Domain enforces rules, application handles errors

**Type safety** - DateOnly for dates, Uri for URLs, immutable collections

This design ensures Geo is a reliable, performant foundation that other services can depend on without tight coupling or availability concerns. The three-tier caching strategy (PostgreSQL → Redis → Local Memory) provides excellent performance while maintaining operational simplicity and graceful degradation under failure conditions.