# Result

Standardized result types for operation outcomes with HTTP-aware status codes and structured error handling. Implements the Result pattern to eliminate exception-based control flow.

## Files

| File Name                                  | Description                                                                                                                       |
| ------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------- |
| [D2Result.cs](D2Result.cs)                 | Non-generic result type with success/failure status, messages, validation errors, and HTTP status codes.                          |
| [D2Result.Generic.cs](D2Result.Generic.cs) | Generic result type `D2Result<TData>` that includes typed data payload along with outcome metadata.                               |
| [ErrorCodes.cs](ErrorCodes.cs)             | Standard error code constants (NOT_FOUND, SOME_FOUND, FORBIDDEN, UNAUTHORIZED, VALIDATION_FAILED, CONFLICT, UNHANDLED_EXCEPTION). |

---

## Usage Examples

### Factory Methods — Non-Generic (`D2Result`)

> **Note:** `traceId` is auto-injected by `BaseHandler` on both .NET and Node.js. Handlers do NOT pass `traceId:` manually — `BaseHandler.InjectTraceId()` sets it automatically if the handler didn't. Only pass `traceId:` explicitly when constructing `D2Result` outside of a handler (e.g., middleware, error mappers).

The non-generic `D2Result` now has full factory parity with `D2Result<T>`. All semantic factories are available on both types.

```csharp
// Success (200 OK)
D2Result.Ok();

// Created (201 Created)
D2Result.Created();

// Not found (404, NOT_FOUND) — default message: "common_errors_NOT_FOUND"
D2Result.NotFound();
D2Result.NotFound(["geo_errors_LOCATION_NOT_FOUND"]);

// Forbidden (403, FORBIDDEN) — default message: "common_errors_FORBIDDEN"
D2Result.Forbidden();

// Conflict (409, CONFLICT) — default message: "common_errors_CONFLICT"
D2Result.Conflict();

// Some found (206, SOME_FOUND) — default message: "common_errors_SOME_FOUND"
D2Result.SomeFound();

// Generic failure (400 Bad Request by default, or specify status/error)
D2Result.Fail(["Something went wrong."], HttpStatusCode.BadGateway);

// Validation failure (400 Bad Request, VALIDATION_FAILED)
D2Result.ValidationFailed(
    inputErrors: [["email", "Email is required."], ["name", "Name too long."]]);

// Unauthorized (401, UNAUTHORIZED)
D2Result.Unauthorized();

// Service unavailable (503, SERVICE_UNAVAILABLE)
D2Result.ServiceUnavailable(["Geo service is down."]);

// Unhandled exception (500, UNHANDLED_EXCEPTION)
D2Result.UnhandledException(["Unexpected error in cache layer."]);

// Payload too large (413, PAYLOAD_TOO_LARGE)
D2Result.PayloadTooLarge(["Request body exceeds 10 MB limit."]);

// Cancelled (400, CANCELLED)
D2Result.Cancelled();
```

### Factory Methods — Generic (`D2Result<T>`)

```csharp
// Success with data (200 OK)
D2Result<LocationDTO>.Ok(locationDto);

// Created with data (201 Created)
D2Result<ContactDTO>.Created(contactDto);

// Not found (404, NOT_FOUND)
D2Result<LocationDTO>.NotFound(["Location not found."]);

// Forbidden (403, FORBIDDEN)
D2Result<OrgDTO>.Forbidden(["Insufficient permissions."]);

// Unauthorized (401, UNAUTHORIZED)
D2Result<UserDTO>.Unauthorized();

// Conflict (409, CONFLICT)
D2Result<ContactDTO>.Conflict(["Contact already exists."]);

// Validation failed (400, VALIDATION_FAILED)
D2Result<ContactDTO>.ValidationFailed(
    inputErrors: [["phone", "Invalid phone number."]]);

// Partial success — some items found, not all (206, SOME_FOUND)
D2Result<List<WhoIsDTO>>.SomeFound(partialResults, ["3 of 5 items found."]);

// Service unavailable (503, SERVICE_UNAVAILABLE)
D2Result<LocationDTO>.ServiceUnavailable();

// Unhandled exception (500, UNHANDLED_EXCEPTION)
D2Result<LocationDTO>.UnhandledException();

// Payload too large (413, PAYLOAD_TOO_LARGE)
D2Result<BatchResult>.PayloadTooLarge(["Batch exceeds 500 items."]);

// Cancelled (400, CANCELLED)
D2Result<LocationDTO>.Cancelled();

// Generic failure (specify status/error manually)
D2Result<LocationDTO>.Fail(["Upstream timeout."], HttpStatusCode.GatewayTimeout);
```

### Error Propagation

```csharp
// BubbleFail — propagate a failure into a different result type (data discarded)
var cacheResult = await r_getFromCache.HandleAsync(new(hashId), ct);
if (cacheResult.CheckFailure(out _))
{
    return D2Result<LocationDTO?>.BubbleFail(cacheResult);
}

// Bubble — propagate any result (success or failure) with new data
var dbResult = await r_repository.HandleAsync(new(ids), ct);
var mapped = dbResult.Data?.Select(e => e.ToDTO()).ToList();
return D2Result<List<LocationDTO>?>.Bubble(dbResult, mapped);
```

### Pattern Matching

```csharp
// CheckSuccess — branch on success and extract data
var result = await r_handler.HandleAsync(input, ct);
if (result.CheckSuccess(out var data))
{
    // Use data (non-null on success)
    return D2Result<OutputDTO?>.Ok(data!.ToDTO());
}

// CheckFailure — branch on failure (data may still be present, e.g. SOME_FOUND)
if (result.CheckFailure(out var partialData))
{
    if (result.ErrorCode == ErrorCodes.SOME_FOUND && partialData is not null)
    {
        // Handle partial success — data is available despite failure flag
        return D2Result<List<ItemDTO>?>.SomeFound(partialData);
    }

    return D2Result<OutputDTO?>.BubbleFail(result);
}
```

### Best Practices

1. **Always use semantic factories** instead of raw `Fail()` with manual status codes. If the outcome is "not found", use `NotFound()` — not `Fail(statusCode: HttpStatusCode.NotFound, errorCode: ErrorCodes.NOT_FOUND)`. The factory sets the correct status code, error code, and default message automatically. Default messages are TK translation key strings (e.g. `"common_errors_NOT_FOUND"`) — the translation middleware resolves them to locale-appropriate text before reaching the client.

2. **Raw `Fail()` is only appropriate** when no semantic factory matches the scenario (e.g., an error handler re-mapping arbitrary upstream status codes, or a status code like `429 Too Many Requests` that has no dedicated factory).

3. **Do NOT pass `traceId` manually inside handlers.** `BaseHandler.InjectTraceId()` auto-injects the ambient `traceId` into every `D2Result` returned from `ExecuteAsync`. Only pass `traceId:` explicitly when constructing `D2Result` **outside** of a handler (e.g., middleware error responses, global exception handlers).

4. **Use `BubbleFail` to propagate errors** across result types without losing metadata (messages, input errors, status code, error code, trace ID). This avoids manually copying fields and prevents accidental data loss during type conversion.

5. **Use `Bubble` (not `BubbleFail`)** when you need to carry data through — for example, mapping domain entities to DTOs while preserving the original result's success/failure state and metadata.
