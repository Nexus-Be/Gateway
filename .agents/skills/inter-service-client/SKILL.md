---
name: inter-service-client
description: Add or modify downstream routing clusters and identity propagation in Nexus YARP Gateway (port 8080). Use when configuring downstream proxy routes to microservices.
---

# Downstream Reverse Proxy Routing in YARP Gateway

> Scope: Gateway repository only. Never reference parent workspace paths, root documentation, or sibling service directories.

## Local Facts

- Service: Nexus Edge Gateway
- Local HTTP Port: 8080
- Technology: Microsoft YARP Reverse Proxy (v2.3.0), Stateless, 0 Database
- Routing Configuration: `Gateway/appsettings.json` (`ReverseProxy:Routes` and `ReverseProxy:Clusters`)
- Identity Transform: `Gateway/Transforms/IdentityPropagationTransform.cs`

## Invariants

1. **Edge YARP Routing (No Manual Controllers):** Gateway does not invoke microservices through typed `HttpClient` services. It forwards client requests downstream via YARP clusters.
2. **Anti-Spoofing & Identity Propagation:**
   - Client requests with untrusted headers (`x-user-id`, `x-user-roles`, `x-internal-service-key`) are rejected by `ClientContextMiddleware` with HTTP 403 `FORBIDDEN_HEADER_SPOOFING`.
   - Gateway verifies JWT at the edge and securely injects validated claims into downstream headers (`x-user-id`, `x-user-roles`) via `AddIdentityPropagation()`.
3. **Plural Route Convention:** Downstream routes must match 100% plural domain paths (`/api/v1/users/**`, `/api/v1/wallets/**`, etc.).
4. **Internal Routes Prohibited:** Never configure YARP routes for `/api/v1/internal/*`.
5. **W3C Distributed Tracing Propagation:** Gateway must register OpenTelemetry and export spans to Tempo (`4317`). Incoming `traceparent` headers must be propagated downstream or generated if absent (`00-{traceId}-{spanId}-01`), ensuring end-to-end distributed tracing across all clusters.

## Downstream Cluster Configuration Example

In `Gateway/appsettings.json`:
```json
{
  "ReverseProxy": {
    "Clusters": {
      "wallet-cluster": {
        "Destinations": {
          "wallet-service": {
            "Address": "http://localhost:8008"
          }
        }
      }
    }
  }
}
```
