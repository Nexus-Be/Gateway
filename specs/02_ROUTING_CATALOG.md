# 02 — YARP Route Catalog & Downstream Cluster Specification

This document maps every public REST prefix exposed by the Gateway to its downstream microservice cluster. The route table is the single source of truth in `Gateway/appsettings.json`, mirrored byte for byte in `Gateway/appsettings.example.json`, and pinned by `Gateway.Tests/RouteConfigurationTests.cs`.

---

## 1. Downstream Clusters

| Cluster Identifier | Destination Address | Target Service |
|:---|:---|:---|
| `user-cluster` | `http://localhost:8001` | UserService |
| `catalog-cluster` | `http://localhost:8002` | CatalogService |
| `commerce-cluster` | `http://localhost:8003` | CommerceService |
| `log-cluster` | `http://localhost:8004` | LogService |
| `auction-cluster` | `http://localhost:8005` | AuctionService |
| `fulfillment-cluster` | `http://localhost:8006` | FulfillmentService |
| `notification-cluster` | `http://localhost:8007` | NotificationService |
| `wallet-cluster` | `http://localhost:8008` | WalletService |
| `checkheal-cluster` | `http://localhost:8009` | CheckHealService |

---

## 2. Inbound Routes

One route per public resource prefix. Every route uses a single `{**catch-all}` pattern, which also matches the bare prefix because an ASP.NET Core catch-all accepts an empty remainder.

| Route Identifier | Match Path | Cluster | Representative Endpoints |
|:---|:---|:---|:---|
| `auth-route` | `/api/v1/auth/{**catch-all}` | `user-cluster` | `POST /api/v1/auth/login`, `POST /api/v1/auth/register`, `POST /api/v1/auth/otp/verify` |
| `user-me-route` | `/api/v1/me/{**catch-all}` | `user-cluster` | `GET /api/v1/me`, `PATCH /api/v1/me` |
| `users-route` | `/api/v1/users/{**catch-all}` | `user-cluster` | `GET /api/v1/users/{userId}` |
| `sellers-route` | `/api/v1/sellers/{**catch-all}` | `user-cluster` | `POST /api/v1/sellers/registration`, `GET /api/v1/sellers/profile` |
| `admin-users-route` | `/api/v1/admin/users/{**catch-all}` | `user-cluster` | `GET /api/v1/admin/users`, `PUT /api/v1/admin/users/{userId}/roles` |
| `admin-roles-route` | `/api/v1/admin/roles/{**catch-all}` | `user-cluster` | `GET /api/v1/admin/roles` |
| `admin-seller-registrations-route` | `/api/v1/admin/seller-registrations/{**catch-all}` | `user-cluster` | `GET /api/v1/admin/seller-registrations`, `PUT .../{id}/approve` |
| `categories-route` | `/api/v1/categories/{**catch-all}` | `catalog-cluster` | `GET /api/v1/categories`, `GET /api/v1/categories/{id}` |
| `products-route` | `/api/v1/products/{**catch-all}` | `catalog-cluster` | `GET /api/v1/products`, `GET /api/v1/products/{id}` |
| `carts-route` | `/api/v1/carts/{**catch-all}` | `commerce-cluster` | `GET /api/v1/carts/active` |
| `orders-route` | `/api/v1/orders/{**catch-all}` | `commerce-cluster` | `GET /api/v1/orders/{id}`, `GET /api/v1/orders/buyer/{buyerId}` |
| `logs-route` | `/api/v1/logs/{**catch-all}` | `log-cluster` | `GET /api/v1/logs`, `GET /api/v1/logs/event-types` |
| `auctions-route` | `/api/v1/auctions/{**catch-all}` | `auction-cluster` | `GET /api/v1/auctions`, `GET /api/v1/auctions/{id}/bids` |
| `warehouses-route` | `/api/v1/warehouses/{**catch-all}` | `fulfillment-cluster` | `GET /api/v1/warehouses`, `GET /api/v1/warehouses/{id}` |
| `shipments-route` | `/api/v1/shipments/{**catch-all}` | `fulfillment-cluster` | `GET /api/v1/shipments/tracking/{trackingNumber}` |
| `notifications-route` | `/api/v1/notifications/{**catch-all}` | `notification-cluster` | `GET /api/v1/notifications/me`, `GET /api/v1/notifications/preferences` |
| `admin-notifications-route` | `/api/v1/admin/notifications/{**catch-all}` | `notification-cluster` | `GET /api/v1/admin/notifications` |
| `admin-notifications-health-route` | `/api/v1/admin/health/{**catch-all}` | `notification-cluster` | `GET /api/v1/admin/health/broker` |
| `wallets-route` | `/api/v1/wallets/{**catch-all}` | `wallet-cluster` | `GET /api/v1/wallets/me`, `POST /api/v1/wallets/transfers` |
| `admin-wallets-route` | `/api/v1/admin/wallets/{**catch-all}` | `wallet-cluster` | `PATCH /api/v1/admin/wallets/withdrawals/{id}` |
| `healthcheck-route` | `/api/v1/healthcheck/{**catch-all}` | `checkheal-cluster` | `GET /api/v1/healthcheck/status`, `GET /api/v1/healthcheck/services` |
| `health-route` | `/health/{**catch-all}` | `checkheal-cluster` | `GET /health/live`, `GET /health/ready` |

---

## 3. Routing Invariants

1. **Plural only.** No singular resource route exists, and none may be added as a route, alias, or path transform.
2. **One pattern per prefix.** A separate `-direct` route is redundant because `{**catch-all}` matches the empty remainder.
3. **No `Transforms`, no `Order`.** Every prefix is literal and disjoint, so path rewriting and precedence numbers have no effect.
4. **Admin is partitioned by domain.** `/api/v1/admin/users`, `/api/v1/admin/roles`, `/api/v1/admin/wallets`, `/api/v1/admin/notifications`, and `/api/v1/admin/health` each reach their own cluster. A single `/api/v1/admin/{**catch-all}` route to UserService is prohibited.
5. **Internal APIs never appear here.** `ClientContextMiddleware` rejects any path containing `/internal/` with HTTP 404 before proxying.
6. **Gateway-local `/health`.** `Program.cs` maps its own `GET /health` endpoint, which takes precedence over `health-route`. Only the deeper probes proxy to CheckHeal.
7. **The table is pinned.** `Gateway.Tests/RouteConfigurationTests.cs` fails when a route is added, removed, or renamed, when a route gains a transform or an ordering number, or when a route exposes a singular resource segment.
