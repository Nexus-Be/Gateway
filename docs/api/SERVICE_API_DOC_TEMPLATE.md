# GATEWAY - [HTTP_METHOD] [ROUTE]

> **Route ID:** {RouteId}
> **Trạng thái:** ⏳ DRAFT (Pre-Config Plan) | 🛠 IN_PROGRESS | ✅ VERIFIED
> **Ngày tạo:** YYYY-MM-DD
> **Tác giả:** {Author / AI Agent}

---

## 1. Thông tin Định tuyến YARP & Bảo mật
- **HTTP Method:** `GET` | `POST` | `PUT` | `PATCH` | `DELETE`
- **Route Path:** `/api/v1/{plural-collection}/...`
- **Cluster Đích:** `{service}-cluster` (ví dụ `user-cluster`, `wallet-cluster`)
- **Downstream URL:** `http://{service-host}:{port}/api/v1/...`
- **Quyền hạn (Authorization Policy):** `AllowAnonymous` | `Authenticated` | `AdminOnly`
- **Rate Limiting Policy:** `None` | `OtpRateLimit` | `AuthRateLimit` (Redis:6379)

---

## 2. Ranh giới Header & Chống Giả mạo Danh tính (Anti-Spoofing & Identity)
- **Header cấm từ bên ngoài:** `x-user-id`, `x-user-email`, `x-user-roles`, `x-internal-service-key`
- **Xử lý vi phạm:** Trả ngay HTTP 403 `FORBIDDEN_HEADER_SPOOFING`.
- **Claims lan truyền downstream (YARP Transform):**
  - `x-user-id` <= JWT `sub`
  - `x-user-email` <= JWT `email`
  - `x-user-roles` <= JWT `role`

---

## 3. Quy chuẩn Đóng gói Response Envelope
Gateway tự động bọc response thành công từ downstream:
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Success",
  "data": { ... },
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "timestamp": "2026-11-01T14:30:00Z"
}
```

---

## 4. Ma trận Mã lỗi Biên (Edge Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt |
| :---: | :--- | :--- | :--- |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this endpoint.` | Thiếu Bearer token hợp lệ |
| `403 Forbidden` | `FORBIDDEN_HEADER_SPOOFING` | `Security violation: Untrusted identity header detected in external request.` | Phát hiện header nhạy cảm gửi từ client |
| `429 Too Many Requests` | `RATE_LIMIT_EXCEEDED` | `Too many requests. Please try again later.` | Vượt quá ngưỡng Rate Limit Redis |
| `503 Service Unavailable` | `SERVICE_UNAVAILABLE` | `Downstream microservice is unreachable or unhealthy.` | Downstream cluster không phản hồi |

---

## 5. Checklist Triển khai Route
- [ ] **Route Config:** Đã thêm Route vào `Gateway/appsettings.json`.
- [ ] **Cluster Config:** Đã chỉ định đúng Cluster ID và Destination URL.
- [ ] **Transforms:** Đã cấu hình PathPattern và IdentityPropagation.
- [ ] **Verify:** `dotnet test Gateway.sln -c Release` vượt qua 100% với 0 lỗi.
