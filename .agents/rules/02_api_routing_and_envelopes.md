# 02 — REVERSE PROXY ROUTING & ENVELOPE ENCAPSULATION (GATEWAY)

## 1. YARP Routing Catalog Standards
* All collection routes forward to plural downstream paths (`/api/v1/users`, `/api/v1/products`, `/api/v1/orders`...).
* Singular public routes are prohibited. Do not add them as YARP routes, aliases, or `Transforms`.
* Every public collection declares exactly one pattern, `/api/v1/{resources}/{**catch-all}`. The catch-all also matches the bare collection path, so a separate direct route is redundant.
* Admin routes partitioned by domain (`/api/v1/admin/users`, `/api/v1/admin/roles`, `/api/v1/admin/wallets`, `/api/v1/admin/notifications`). Prohibit a single catch-all admin route to UserService.
* `Gateway.Tests/RouteConfigurationTests.cs` pins the route table. Update the test and both `appsettings.json` files in the same change.

## 2. Automatic Response Envelope Middleware
* Intercepts successful downstream responses (HTTP 200 and 201) with `application/json` content type.
* Encapsulates raw payload into `ApiResponse<T>`:
  * `success = true`
  * `statusCode = response.StatusCode`
  * `message = customMessage ?? "Operation completed successfully."` (dynamically extracted if downstream JSON contains `"message"`)
  * `data = root`
  * `traceId = Activity.Current?.Id ?? context.TraceIdentifier`
  * `timestamp = UtcNow`
* **Passthrough Exceptions**:
  * HTTP `204 NoContent`: Passthrough 0 bytes directly without JSON envelope.
  * HTTP `>= 300` (Errors): Passthrough ProblemDetails RFC 9110 directly without wrapping.
  * Already enveloped: If downstream payload contains property `success`, pass through directly to avoid double-wrapping.

## 3. Zero Pagination Calculation Invariant
* Gateway never computes pagination or buffers raw collection data in RAM to calculate total counts or page slices.
