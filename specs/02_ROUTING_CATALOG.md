# 02 — YARP Route Catalog & Downstream Cluster Specification

This document maps all public REST endpoints exposed through the Gateway to their corresponding downstream microservice clusters and port bindings.

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

## 2. Inbound Routes & Path Transforms

| Route Identifier | Inbound Match Path | Destination Cluster | Transforms / Notes |
|:---|:---|:---|:---|
| `admin-route` | `/api/v1/admin/{**catch-all}` | `user-cluster` | Rewrites path to `/api/v1/admin/user/{**catch-all}` |
| `auth-route` | `/api/v1/auth/{**catch-all}` | `user-cluster` | Public registration, login, and OTP |
| `user-me-route` | `/api/v1/me/{**catch-all}` | `user-cluster` | Authenticated profile and sessions |
| `user-me-direct` | `/api/v1/me` | `user-cluster` | Direct user profile route |
| `user-route` | `/api/v1/user/{**catch-all}` | `user-cluster` | Singular convention for user management |
| `seller-route` | `/api/v1/sellers/{**catch-all}` | `user-cluster` | Seller registration and onboarding |
| `catalog-categories-route` | `/api/v1/categories/{**catch-all}` | `catalog-cluster` | Category hierarchy |
| `catalog-products-route` | `/api/v1/products/{**catch-all}` | `catalog-cluster` | Product catalog and variants |
| `commerce-carts-route` | `/api/v1/carts/{**catch-all}` | `commerce-cluster` | Shopping carts and item lines |
| `commerce-orders-route` | `/api/v1/orders/{**catch-all}` | `commerce-cluster` | Order placement and cancellation |
| `log-route` | `/api/v1/logs/{**catch-all}` | `log-cluster` | Central audit and event querying |
| `auction-auctions-route` | `/api/v1/auctions/{**catch-all}` | `auction-cluster` | Auction rooms and states |
| `auction-bids-route` | `/api/v1/bids/{**catch-all}` | `auction-cluster` | Bidding activity |
| `fulfillment-warehouses-route` | `/api/v1/warehouses/{**catch-all}` | `fulfillment-cluster` | Warehouse management |
| `fulfillment-shipments-route` | `/api/v1/shipments/{**catch-all}` | `fulfillment-cluster` | Shipment tracking |
| `notification-notifications-route` | `/api/v1/notifications/{**catch-all}` | `notification-cluster` | Push notifications and user inbox |
| `notification-email-route` | `/api/v1/email/{**catch-all}` | `notification-cluster` | Internal email dispatching |
| `wallet-wallets-route` | `/api/v1/wallets/{**catch-all}` | `wallet-cluster` | Wallet accounts, balances, transactions |
| `checkheal-route` | `/api/v1/checkheal/{**catch-all}` | `checkheal-cluster` | Health keep-alive probes and toggles |
| `checkheal-health-route` | `/health/{**catch-all}` | `checkheal-cluster` | System health checks |

---

## 3. Administrative Path Normalization Invariant

All external management calls use the singular admin path:
```http
GET /api/v1/admin/seller-registrations
PUT /api/v1/admin/seller-registrations/{id}/approve
```

The YARP route transform transparently maps these to UserService:
```http
PathPattern: /api/v1/admin/user/{**catch-all}
```
Client applications remain completely decoupled from the internal controller naming convention.
