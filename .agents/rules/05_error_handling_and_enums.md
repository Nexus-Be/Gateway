# 05 — ERROR HANDLING & API ERROR RESPONSES (GATEWAY)

## 1. 100% English & Sentence Case
* Error messages must be 100% English sentence case.
* Capitalize only the first letter. Keep technical acronyms uppercase (`ID`, `OTP`, `JWT`, `URL`, `IP`).
* Examples:
  * `"Too many requests. Please slow down and try again later."`
  * `"Authentication is required to access administrative endpoints."`
  * `"Idempotency-Key header value is invalid or exceeds maximum length."`

## 2. SCREAMING_SNAKE_CASE Error Codes
* Standard error codes:
  * `FORBIDDEN_HEADER_SPOOFING`
  * `UNAUTHORIZED`
  * `FORBIDDEN`
  * `NOT_FOUND`
  * `VAL_INVALID_INPUT`
  * `RATE_LIMIT_EXCEEDED`
  * `INTERNAL_SERVER_ERROR`

## 3. ApiErrorResponse Structure (RFC 9110 ProblemDetails)
* Gateway generated errors conform to RFC 9110 ProblemDetails:
  ```json
  {
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
    "title": "Forbidden",
    "status": 403,
    "errorCode": "FORBIDDEN_HEADER_SPOOFING",
    "detail": "Security violation: Untrusted identity header detected in external request.",
    "traceId": "0HNPGT7G285Q2",
    "timestamp": "2026-10-31T05:30:00Z"
  }
  ```
