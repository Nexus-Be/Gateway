# Nexus API Gateway

High-performance, lightweight Edge API Gateway built on Microsoft YARP for the Nexus microservice ecosystem.

---

## 1. Overview

The Gateway acts as the single entry point for all incoming client traffic at port `8080`. It handles:
- **Streaming Reverse Proxy:** High-throughput HTTP forwarding using Microsoft YARP, eliminating redundant proxy controllers and duplicate DTO models.
- **Edge Authentication & Identity Propagation:** Terminates client JWT Bearer tokens and injects verified claims (`x-user-id`, `x-user-email`, `x-user-roles`, `x-user-permissions`, `x-client-ip`) into downstream headers.
- **Global Response Envelope:** Intercepts raw microservice JSON responses and encapsulates them into the standard `ApiResponse<T>` structure with `traceId` and UTC timestamps.
- **Strict Route Rate Limiting:** Enforces Redis-backed rate limiting on sensitive authentication operations (OTP verification, registration, login).
- **Administrative Path Normalization:** Exposes `/api/v1/admin/{**catch-all}` externally and transforms it downstream to `/api/v1/admin/user/{**catch-all}`.
- **Anti-Spoofing Security:** Strips all inbound `x-user-*` headers from external clients before forwarding.

---

## 2. Documentation Index

The documentation follows the Diátaxis framework:

- **How-to Guide:** [docs/HOW_TO_GUIDE.md](docs/HOW_TO_GUIDE.md) provides step-by-step instructions for running the gateway, adding new clusters, declaring routes, and testing.
- **Architecture Explanation:** [docs/EXPLANATION_ARCHITECTURE.md](docs/EXPLANATION_ARCHITECTURE.md) explains why YARP was chosen, why middlewares are partitioned, and how identity propagation works.
- **Architecture Specification:** [specs/01_ARCHITECTURE.md](specs/01_ARCHITECTURE.md) defines the pipeline execution sequence and security invariants.
- **Routing Catalog:** [specs/02_ROUTING_CATALOG.md](specs/02_ROUTING_CATALOG.md) lists the complete route-to-cluster mapping and port catalog.

---

## 3. Port & Downstream Service Map

| Service Name | Port | Downstream Route Prefix |
|:---|:---:|:---|
| **Gateway (Host)** | `8080` | `/api/v1/**` |
| **UserService** | `8001` | `/api/v1/auth`, `/api/v1/user`, `/api/v1/admin`, `/api/v1/me`, `/api/v1/sellers` |
| **CatalogService** | `8002` | `/api/v1/categories`, `/api/v1/products` |
| **CommerceService** | `8003` | `/api/v1/carts`, `/api/v1/orders` |
| **LogService** | `8004` | `/api/v1/logs` |
| **AuctionService** | `8005` | `/api/v1/auctions`, `/api/v1/bids` |
| **FulfillmentService** | `8006` | `/api/v1/warehouses`, `/api/v1/shipments` |
| **NotificationService** | `8007` | `/api/v1/notifications`, `/api/v1/email` |
| **WalletService** | `8008` | `/api/v1/wallets` |
| **CheckHealService** | `8009` | `/health`, `/api/v1/checkheal` |

---

## 4. Environment Variables

| Variable | Default Value | Description |
|:---|:---|:---|
| `ASPNETCORE_ENVIRONMENT` | `Development` | ASP.NET Core environment mode. |
| `ASPNETCORE_URLS` | `http://+:8080` | HTTP listening URL and port. |
| `Jwt__Issuer` | `http://localhost:8001` | Expected JWT token issuer. |
| `Jwt__Audience` | `shoppingcart-api` | Expected JWT token audience. |
| `Jwt__Signature` | `SuperSecret...` | Secret key used to sign and verify JWT tokens. |
| `Redis__ConnectionString` | `localhost:6379` | Shared Redis connection string for distributed rate limiting. |

---

## 5. Getting Started

### Build
```bash
dotnet build Gateway.sln -c Release
```

### Run
```bash
dotnet run --project Gateway/Gateway.csproj
```

### Test
```bash
dotnet test Gateway.sln -c Release
```
