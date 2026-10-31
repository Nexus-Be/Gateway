# 03 — ANTI-SPOOFING, IDENTITY PROPAGATION & ROUTE BLOCKING (GATEWAY)

## 1. Active Anti-Header-Spoofing
* `ClientContextMiddleware` scans all incoming external headers (case-insensitive).
* If client submits any forbidden identity or internal key header:
  * `x-user-id`, `x-user-email`, `x-user-roles`, `x-user-role`, `x-user-permissions`, `x-user-permission`, `x-internal-service-key`
* Immediately terminate pipeline and return HTTP `403 Forbidden`:
  * `errorCode: "FORBIDDEN_HEADER_SPOOFING"`
  * `message: "Security violation: Untrusted identity header detected in external request."`

## 2. Strict Internal Route Isolation
* Gateway actively rejects any request with path starting with `/api/v1/internal` or containing `/internal/` with HTTP `404 Not Found` (`NOT_FOUND`).
* Prohibit configuring any YARP route pointing to `/internal/*`.

## 3. Trusted Identity Propagation
* `IdentityPropagationTransform` extracts claims from verified JWT Bearer token:
  * Injects `x-user-id`, `x-user-email`, `x-user-roles`, `x-user-permissions`, `x-client-ip`, `x-trace-id`.
* Downstream microservices rely unconditionally on these injected headers.
