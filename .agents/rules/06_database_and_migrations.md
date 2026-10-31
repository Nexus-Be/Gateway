# 06 — ZERO DATABASE & SHARED INFRASTRUCTURE (GATEWAY)

## 1. Zero Database Invariant
* Gateway is a stateless edge reverse proxy.
* Zero local database: do NOT create DbContext, migrations, seed classes, or local database containers in Gateway.

## 2. Central Shared Infrastructure
* Distributed rate limiting connects exclusively to central shared Redis at port `6379`.
* Keys prefixed with `nexus:gateway:ratelimit:*` to prevent collisions with microservice keys (`otp:*`, `user:*`).
* Fail-Open Policy: In `Development`, if Redis is unavailable, requests are allowed through to prevent local developer disruption.
