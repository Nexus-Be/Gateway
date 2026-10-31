# 01 — Gateway Architectural Blueprint & Security Pipeline

This specification describes the internal architecture, middleware execution sequence, and security invariants implemented in the YARP Gateway.

---

## 1. Middleware Pipeline Ordering

All HTTP requests traverse through an ordered, layered pipeline before reaching the YARP proxy engine:

```text
Incoming Client Request
  │
  ▼
1. GlobalExceptionMiddleware        --> Catches unhandled errors into ApiErrorResponse
  │
  ▼
2. ClientContextMiddleware          --> Active Anti-Spoofing (403), extracts IP & TraceId, internal path block temporarily disabled
  │
  ▼
3. UseCors("AllowFrontend")         --> Handles CORS preflight and access control
  │
  ▼
4. UseAuthentication & Authorization--> Validates JWT Bearer token into ClaimsPrincipal
  │
  ▼
5. SensitiveRateLimitMiddleware     --> Enforces Redis fixed-window limits on OTP/auth routes
  │
  ▼
6. AdminAuthorizationGuard          --> Enforces admin/staff role requirement on /api/v1/admin/**
  │
  ▼
7. IdempotencyGuardMiddleware       --> Validates Idempotency-Key header on mutating operations
  │
  ▼
8. ResponseEnvelopeMiddleware       --> Buffers and wraps downstream 200/201 JSON into ApiResponse<T>
  │
  ▼
9. YARP ReverseProxy Engine         --> Applies request transforms and streams to downstream microservice
```

---

## 2. Global Response Envelope Standard

The Gateway enforces a unified data envelope on all successful read and write operations. Microservices can return raw domain DTOs, and the Gateway encapsulates them prior to sending data to the client:

### Single Entity or Collection Response
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Operation completed successfully.",
  "data": {
    "id": "11111111-1111-1111-1111-000000000004",
    "fullName": "Nguyen Van A",
    "email": "vana@example.com",
    "status": "Active"
  },
  "traceId": "0HNPGT7G285Q2",
  "timestamp": "2026-10-30T13:00:00Z"
}
```

### Paginated Collection Response (`ApiResponse<PagedData<T>>`)
Database-level pagination calculation (`OFFSET/LIMIT` and `COUNT(*)`) is 100% the responsibility of downstream microservices. Microservices return the raw paged payload containing `items` and `pagination`, and the Gateway encapsulates it into `data`:
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Operation completed successfully.",
  "data": {
    "items": [
      { "id": "22222222-2222-2222-2222-000000000001", "name": "Mechanical Keyboard", "price": 1250000.00 }
    ],
    "pagination": {
      "pageIndex": 1,
      "pageSize": 20,
      "totalCount": 245,
      "totalPages": 13,
      "hasPreviousPage": false,
      "hasNextPage": true
    }
  },
  "traceId": "0HNPGT7G285Q2",
  "timestamp": "2026-10-30T13:00:00Z"
}
```
*Note:* The Gateway is a stateless reverse proxy and strictly prohibited from performing database queries or in-memory pagination splitting on unbounded datasets.

### Empty Operations
Empty operations returning HTTP `204 NoContent` pass through with an empty body according to RFC 9110.

### Error Envelope
Client-facing errors and validation failures adhere to `ApiErrorResponse`:
```json
{
  "success": false,
  "statusCode": 400,
  "errorCode": "VAL_INVALID_INPUT",
  "message": "One or more validation errors occurred.",
  "traceId": "0HNPGT7G285Q2",
  "timestamp": "2026-10-30T13:00:00Z"
}
```

---

## 3. Identity Propagation & Security Guards

1. **Active Anti-Spoofing & Rejection:** `ClientContextMiddleware` actively inspects incoming requests for untrusted identity or internal headers (`x-user-id`, `x-user-email`, `x-user-roles`, `x-user-role`, `x-user-permissions`, `x-user-permission`, `x-internal-service-key`). If detected from an external client, the request is immediately rejected with HTTP `403 Forbidden` (`errorCode: "FORBIDDEN_HEADER_SPOOFING"`). External callers cannot forge identity under any circumstances.
2. **Transform Injection:** If the caller presents a valid JWT Bearer token, `IdentityPropagationTransform` extracts:
   - `x-user-id`: Caller unique identifier UUID.
   - `x-user-email`: Caller email address.
   - `x-user-roles`: Comma-separated lowercase roles (e.g. `buyer,seller`).
   - `x-user-permissions`: Comma-separated uppercase permissions.
   - `x-client-ip`: Client remote IP address.
   - `x-trace-id`: Distributed trace identifier.
3. **Sensitive Route Rate Limiting:** High-risk authentication routes enforce atomic counter checks via Redis:
   - `POST /api/v1/auth/otp/resend`: 3 requests per 60s
   - `POST /api/v1/auth/otp/verify`: 5 requests per 60s
   - `POST /api/v1/auth/register`: 5 requests per 300s
   - `POST /api/v1/auth/login`: 10 requests per 60s
