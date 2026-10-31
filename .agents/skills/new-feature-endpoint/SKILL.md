---
name: new-feature-endpoint
description: Add or change a Gateway route in the Nexus YARP gateway (port 8080). Use when adding a public route, an admin route, a cluster destination, a route transform, or when changing anti-spoofing, envelope, rate-limit, or idempotency behavior.
---

# New Gateway Route

> Scope: this repository only. Never reference the parent workspace, its documentation, or a sibling service.
> Synced with the root `new-feature-endpoint` skill on 2026-10-31.

## Local facts

- Service: Nexus API Gateway
- HTTP port: 8080
- Database: none. The Gateway is stateless.
- Solution: `Gateway.sln`
- Tests: `Gateway.Tests` (xUnit)
- Controllers: none. Routing is YARP configuration.
- Route catalog: `Gateway/appsettings.json` and `Gateway/appsettings.example.json`
- Architecture spec: `specs/01_ARCHITECTURE.md`
- Routing spec: `specs/02_ROUTING_CATALOG.md`
- Guides: `docs/HOW_TO_GUIDE.md` and `docs/EXPLANATION_ARCHITECTURE.md`

## Read first

- `README.md`.
- `.agents/rules/01_clean_architecture_layers.md` through `.agents/rules/06_database_and_migrations.md`.
- `specs/01_ARCHITECTURE.md` and `specs/02_ROUTING_CATALOG.md`.
- `docs/HOW_TO_GUIDE.md` and `docs/EXPLANATION_ARCHITECTURE.md`.
- `Gateway/appsettings.json`.
- `Gateway/Middlewares/Global/ClientContextMiddleware.cs` and `Gateway/Middlewares/Global/ResponseEnvelopeMiddleware.cs`.
- `Gateway/Transforms/IdentityPropagationTransform.cs`.
- `Gateway.Tests`.

## Checklist

### 0. Preflight

- [ ] Read the files above.
- [ ] Run GitNexus impact for every symbol or configuration key to change and record the risk.
- [ ] Confirm the downstream service, cluster, port, public path, and auth policy.
- [ ] Confirm whether the route is public, admin, or sensitive (auth, OTP).
- [ ] Create `.agents/plan/PLAN-YYYYMMDD-HHMM-<NAME>.md`.
- [ ] Stop and ask if the route contract is ambiguous.

### 1. Contract

- [ ] Public path is `/api/v1/{plural-resource}/{**catch-all}`.
- [ ] Admin path is `/api/v1/admin/{domain}/{**catch-all}` and targets the owning service.
- [ ] No single admin catch-all that swallows every domain.
- [ ] No route for `/api/v1/internal` or any path containing `/internal/`.
- [ ] The route has a unique id and a cluster id that already exists or is added in the same change.
- [ ] The route order does not shadow a more specific route.

### 2. Route configuration

- [ ] Add the route and cluster to `Gateway/appsettings.json`.
- [ ] Add the same entries to `Gateway/appsettings.example.json`.
- [ ] Cluster destination uses an environment-driven address, not a hardcoded production host.
- [ ] Do not add secrets to `appsettings.json`.
- [ ] Keep `AuthorizationPolicy`, rate-limit policy, and transforms consistent with neighboring routes.

### 3. Middleware behavior

- [ ] `ClientContextMiddleware` still rejects spoofed identity headers with `403 FORBIDDEN_HEADER_SPOOFING`.
- [ ] `ClientContextMiddleware` still strips `X-Internal-Service-Key` from external requests.
- [ ] `IdentityPropagationTransform` still injects `x-user-id`, `x-user-email`, `x-user-roles`, and `x-user-permissions` from the validated token only.
- [ ] `ResponseEnvelopeMiddleware` still wraps a successful downstream DTO into `ApiResponse<T>`.
- [ ] Pagination passes through untouched; the Gateway never computes pages.
- [ ] `AdminAuthorizationGuardMiddleware` still protects admin routes.
- [ ] `SensitiveRateLimitMiddleware` still covers auth and OTP routes.

### 4. Tests

- [ ] Add or update a route test in `Gateway.Tests`.
- [ ] Cover the public path, the admin path, and the missing-route `404`.
- [ ] Cover envelope wrapping for the new route.
- [ ] Cover the anti-spoofing rejection when identity headers are sent from outside.
- [ ] Confirm the Swagger aggregation hides internal paths.
- [ ] Run `dotnet test Gateway.sln -c Release`.

### 5. Verify

- [ ] `dotnet build Gateway.sln -c Release` has 0 errors.
- [ ] `dotnet test Gateway.sln -c Release` passes 100%.
- [ ] Fetch `/swagger/v1/swagger.json` and confirm the new path appears in the Gateway document.
- [ ] Curl the route through `:8080` when the contract changed.
- [ ] Curl the route through `:8080` and verify status, envelope, and auth behavior.
- [ ] Confirm `git status` has no build output or secret.
- [ ] Run GitNexus `detect_changes` before commit.

### 6. Handoff

- [ ] Update `specs/02_ROUTING_CATALOG.md`.
- [ ] Tick the session plan.
- [ ] Append the session summary to `.agents/history/YYYY-MM-DD_session_summary.md`.
- [ ] Fill the contract handoff block (service, endpoint, controller, application, entity, RBAC, request, response, errors, enums, tests, build) and give it to the repository owner.
- [ ] Do not link outward from this repository.

## Variant

The Gateway is a pure YARP reverse proxy. It has no controller, no DbContext, no domain logic, and no pagination math. Do not add a `GatewayBaseController`, `ForwardAsync`, or a downstream DTO just to route a request. A route change is a configuration change plus tests and documentation.

## Commands

```bash
dotnet build Gateway.sln -c Release
dotnet test Gateway.sln -c Release
```

## Done when

- [ ] Route and cluster exist in both `appsettings.json` and the example file.
- [ ] No `/internal` route exists.
- [ ] Anti-spoofing, identity propagation, envelope, and rate-limit behavior are verified.
- [ ] Build and tests pass.
- [ ] Routing catalog is updated.
- [ ] The repository owner has the contract handoff block.
