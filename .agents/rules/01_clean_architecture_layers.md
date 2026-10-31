# 01 — EDGE GATEWAY ARCHITECTURE & PIPELINE (GATEWAY)

## 1. Bounded Context & Port Allocation
* Role: Edge API Gateway, external ingress, reverse proxy, authentication, rate limiting, and response envelope encapsulation.
* Port: Fixed at `8080`.
* Local Database: Zero (0 Database, 0 Domain Entities, 0 Controllers, 0 Stateful Logic).

## 2. Microsoft YARP Reverse Proxy Architecture
* Powered by Microsoft YARP (Yet Another Reverse Proxy) v2.3.0.
* Routes traffic directly to 9 backend clusters (`user-cluster:8001` to `checkheal-cluster:8009`).
* High-performance zero-copy streaming pipeline over `System.IO.Pipelines`.

## 3. 9-Layer Middleware Pipeline Order
1. `GlobalExceptionMiddleware`: Top-level safety net returning unified `ApiErrorResponse` on unhandled 500 exceptions.
2. `ClientContextMiddleware`: Active Anti-Header-Spoofing & strict `/api/v1/internal/*` blocker.
3. `AuthenticationMiddleware` & `JwtBearerHandler`: Edge JWT signature verification.
4. `AdminAuthorizationGuardMiddleware`: Edge coarse-grained RBAC guard for `/api/v1/admin/**`.
5. `IdempotencyGuardMiddleware`: Validates `Idempotency-Key` format and length on mutating HTTP methods.
6. `SensitiveRateLimitMiddleware`: Distributed Redis rate limiting on sensitive auth/OTP routes.
7. `IdentityPropagationTransform`: Injects verified claims downstream (`x-user-id`, `x-user-roles`, etc.).
8. `MapReverseProxy`: YARP forwarder to downstream microservices.
9. `ResponseEnvelopeMiddleware`: Intercepts downstream 200/201 JSON responses and wraps into `ApiResponse<T>`.

## 4. Absolute Architectural Prohibitions
* 🛑 Prohibit controllers or MVC actions inside Gateway.
* 🛑 Prohibit DbContext or database connections inside Gateway.
* 🛑 Prohibit computing pagination at Gateway.
