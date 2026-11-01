---
name: controller-conventions
description: Architectural conventions for edge HTTP routing, envelope encapsulation, and zero-controller invariant in Nexus API Gateway (port 8080). Explains why controllers are prohibited and how YARP reverse proxy handles downstream endpoints.
---

# Controller Conventions & Zero-Controller Invariant in Nexus Gateway

> Scope: Gateway repository only. Never reference parent workspace paths, root documentation, or sibling service directories.

This skill defines the architectural boundary standards and the **Zero-Controller Invariant** for the Nexus Edge API Gateway.

---

## 0. Zero-Controller Invariant (Architectural Boundary)

In the Nexus ecosystem, the Edge API Gateway is a **High-Performance Stateless Reverse Proxy** built on Microsoft YARP:

- **Zero Controllers by Design**: Gateway contains **NO ASP.NET Core controllers** and **NO database contexts**. Creating redundant proxy controllers in Gateway (e.g. `UserController`, `ProductController`) is strictly prohibited.
- **Why Controllers are Prohibited**: Controllers in an edge gateway introduce redundant deserialization/re-serialization CPU overhead, duplicate DTO classes across boundaries, and introduce latency bottlenecks.
- **YARP Streaming Direct Forwarding**: All `/api/v1/**` client requests are streamed directly to downstream microservices via Microsoft YARP reverse proxy configuration in `Gateway/appsettings.json`.

---

## 1. When to Use

Consult and follow this skill whenever you:
- Consider adding an endpoint, controller, or action to Gateway.
- Configure YARP route catalogs or transforms.
- Modify the global response envelope (`ResponseEnvelopeMiddleware`).
- Work with edge anti-spoofing and identity propagation (`ClientContextMiddleware`, `IdentityPropagationTransform`).

---

## 2. Downstream Controller Contract Alignment

While Gateway contains no controllers of its own, it enforces and relies on the controller conventions of downstream microservices:

### Pure Payload Contract
- Downstream microservice controllers return **pure DTO payloads** (e.g. `Ok(userDto)` or `Ok(pagedData)`), or `NoContent()` (HTTP 204).
- Downstream controllers **never** wrap responses in `ApiResponse<T>`.

### Edge Response Envelope Wrapping
- Gateway's `ResponseEnvelopeMiddleware` intercepts all successful downstream responses (200, 201) and encapsulates them into the standard outer envelope:
  ```json
  {
    "success": true,
    "statusCode": 200,
    "message": "Operation completed successfully.",
    "data": { ... },
    "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
    "timestamp": "2026-11-01T07:05:00Z"
  }
  ```
- HTTP 204 NoContent responses pass through untouched with a 0-byte body.

---

## 3. Anti-Spoofing & Identity Propagation Invariants

- **Active Anti-Spoofing Rejection**:
  `ClientContextMiddleware` actively inspects all incoming external headers. If any untrusted identity headers (`x-user-id`, `x-user-email`, `x-user-roles`, `x-user-permissions`, `x-internal-service-key`) are detected, Gateway immediately aborts the request and returns HTTP `403 Forbidden` (`errorCode: "FORBIDDEN_HEADER_SPOOFING"`).
- **Trusted Identity Injection**:
  Only Gateway validates client JWT Bearer tokens and propagates verified identity headers downstream via `IdentityPropagationTransform`:
  - `x-user-id`
  - `x-user-email`
  - `x-user-roles`
  - `x-user-permissions`
  - `x-client-ip`

---

## 4. Error Response Formatting

When Gateway rejects requests or encounters downstream connection failures:
- Messages are **100% English Sentence Case** (`"Security violation: Untrusted identity header detected in external request."`, `"The requested downstream service is currently unavailable."`).
- Error codes are **SCREAMING_SNAKE_CASE** (`FORBIDDEN_HEADER_SPOOFING`, `SERVICE_UNAVAILABLE`, `RATE_LIMIT_EXCEEDED`, `UNAUTHORIZED`).
- Formatted as standard RFC 9110 ProblemDetails via `ApiProblemDetails`.
