---
name: service-api-doc
description: Pre-code API documentation standard for Nexus YARP Gateway (port 8080). Mandates drafting a gateway-level routing specification before configuring new downstream routes, clusters, rate-limiting policies, or transforms. Governs YARP reverse-proxy route contracts, anti-spoofing headers, and response envelope wrapping.
---

# Gateway API Pre-Code Documentation Skill

> **Scope:** Gateway repository only (`src/Services/Gateway/`). 100% Inward-Only. Never reference the root workspace or sibling services.
> **Bất biến 0 Database:** Gateway là Edge Reverse Proxy phi trạng thái (Stateless), chạy cổng 8080, không chứa database hay domain business logic.

This skill governs the **mandatory pre-configuration documentation lifecycle** within Gateway. Before any new YARP route, transform, cluster, or rate-limiting policy is added to `Gateway/appsettings.json`, the developer or AI agent must create a technical route specification inside `docs/api/`.

---

## 1. Ranh giới Tài liệu Gateway

* **Vị trí lưu trữ:** `docs/api/Routes/{HTTP_METHOD}_{ROUTE_SLUG}.md` (ví dụ `docs/api/Routes/POST_API_V1_USERS.md`).
* **Thời điểm lập:** **BẮT BUỘC TRƯỚC KHI CẤU HÌNH ROUTE (Pre-Code/Pre-Config)**.
* **Định dạng dữ liệu trả về:** Tự động bọc thành `ApiResponse<T>` hoặc `ApiResponse<PagedData<T>>` qua `ResponseEnvelopeMiddleware`.
* **Rào chắn Inward-Only:** Chỉ trỏ tới tài nguyên nội bộ của Gateway (`Gateway.sln`, `Gateway/appsettings.json`, `Gateway/Middlewares/`). Không tham chiếu tới `docs/api/` ở Root.

---

## 2. Quy trình 3 Bước Tra cứu Source of Truth Trước Khi Lập Doc

### Bước 1: Tra cứu Danh mục Cụm Downstream (Clusters)
- Mở `Gateway/appsettings.json` tại mục `ReverseProxy:Clusters`.
- Xác định cluster đích (`user-cluster:8001`, `catalog-cluster:8002`, `commerce-cluster:8003`, `wallet-cluster:8008`...).

### Bước 2: Tra cứu Chính sách Bảo mật & Rate Limiting
- Xác định route có cần Rate Limiting phân tán qua Redis không (`RedisRateLimiting`).
- Xác định yêu cầu xác thực JWT Bearer tại biên và chính sách RBAC (`AllowAnonymous`, `RequireAuthenticatedUser`, `AdminOnly`).

### Bước 3: Tra cứu Transform & Identity Propagation
- Kiểm tra các header nhạy cảm cần tiêm an toàn: `x-user-id`, `x-user-email`, `x-user-roles`.
- Xác nhận cơ chế Anti-Spoofing: Chặn đứng mọi client bên ngoài tự gửi `x-user-*` hoặc `x-internal-service-key`.

---

## 3. Các Rào Chắn Bất Biến tại Gateway

1. **Zero Pagination Calculation:** Gateway tuyệt đối không nhận dữ liệu thô lớn để tự cắt trang. Toàn bộ phân trang tính toán tại downstream service.
2. **Plural Route Convention:** 100% route collections là số nhiều (`/api/v1/users`, `/api/v1/products`). Route admin phân tách theo domain (`/api/v1/admin/users/**`, `/api/v1/admin/wallets/**`). Cấm route admin gộp.
3. **Active Anti-Spoofing:** Chặn ngay lập tức request chứa `x-user-*` từ client bên ngoài, trả HTTP 403 `FORBIDDEN_HEADER_SPOOFING`.
4. **Envelope Wrapping:** Tự động đóng gói payload downstream thành `ApiResponse<T>` với `success: true`, `statusCode`, `data`, `traceId`, `timestamp`.

---

## 4. Quy trình Thực hiện (Workflow)

1. Sao chép `docs/api/SERVICE_API_DOC_TEMPLATE.md` vào `docs/api/Routes/{HTTP_METHOD}_{ROUTE_SLUG}.md`.
2. Điền đầy đủ thông tin routing, cluster, rate limiting, và anti-spoofing.
3. Cập nhật `appsettings.json` và cấu hình middleware.
4. Chạy `dotnet test Gateway.sln -c Release` đạt 100% pass, tích checklist và chuyển trạng thái doc sang `✅ VERIFIED`.
