# 04 — AUDIT LOGGING & INGRESS TELEMETRY (GATEWAY)

## 1. Security Ingress Audit Logging
* Log security events with Client IP and TraceId:
  * Blocked header spoofing attempts (IP, spoofed header names, target path).
  * Internal API path-block attempts are not logged while the block is disabled; log them again only after the block is re-enabled.
  * Unauthorized admin endpoint access attempts.
  * Rate-limit threshold violations (`RATE_LIMIT_EXCEEDED`).

## 2. Distributed Tracing & OpenTelemetry
* Ensure W3C `traceparent` and `x-trace-id` headers are generated or propagated on every inbound request.
* Forward trace context to downstream microservices and export OTLP telemetry to Tempo.
