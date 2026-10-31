# API GATEWAY — AI AGENT MISSION PROTOCOL & RULES

> **Service:** Nexus Edge API Gateway (Port 8080, Microsoft YARP Reverse Proxy, 0 Database)
> **Scoping:** 100% Inward-Only. Never reference outward to root or sibling services.

## Local Governance Rules
Every AI Coding Agent working inside ApiGateway must strictly comply with the local rules in `.agents/rules/`:
1. [01 Edge Architecture & Pipeline](rules/01_clean_architecture_layers.md): Microsoft YARP v2.3.0, 9-layer middleware pipeline, 0 database invariant.
2. [02 Routing & Envelopes](rules/02_api_routing_and_envelopes.md): YARP routing catalog, automatic `ApiResponse<T>` envelope wrapping, zero pagination.
3. [03 Anti-Spoofing & Security](rules/03_security_headers_and_internal.md): Active anti-spoofing rejection, identity propagation, strict `/api/v1/internal/*` blocking.
4. [04 Audit & Telemetry](rules/04_audit_and_event_logging.md): Ingress security audit logging, distributed tracing (W3C TraceId, OTLP to Tempo).
5. [05 Error Handling & Codes](rules/05_error_handling_and_enums.md): 100% English sentence-case messages, SCREAMING_SNAKE_CASE error codes, `ApiErrorResponse` structure.
6. [06 Infrastructure & Rate Limiting](rules/06_database_and_migrations.md): Central shared Redis (`6379`), distributed rate limiting, fail-open policy.

## Verification Checklist
- `dotnet build Gateway.sln -c Release` must succeed with 0 errors.
- `dotnet test Gateway.sln -c Release` must pass 100%.
