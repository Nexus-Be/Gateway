# QUY CHUẨN VẬN HÀNH DÀNH CHO AI CODING AGENTS (NEXUS YARP GATEWAY)

> **Trạng thái triển khai (2026-10-30):** ✅ ĐÃ CHUẨN HÓA — YARP Reverse Proxy độc lập (cổng `8080`), 0 Database, cơ chế xác thực JWT tại biên, Rate Limiting phân tán qua Redis, bảo vệ danh tính (Identity Propagation), tự động đóng gói Response Envelope `ApiResponse<T>` / `ApiResponse<PagedData<T>>`.
> **Nguồn sự thật:** code (`Gateway.sln`, `Gateway/Program.cs`, `Gateway/Middlewares/`, `Gateway/appsettings.json`).
> **Cập nhật lần cuối:** 2026-10-30.

---

## Endpoint Workflow

Before adding or changing any REST endpoint, follow the local checklist at `.agents/skills/new-feature-endpoint/SKILL.md`. It covers contract, persistence, Application, Api, tests, documentation handoff, and verification. This repository is Inward-Only, so the checklist is self-contained.

---

## 🛑 NGUYÊN TẮC 1: BẮT BUỘC LẬP KẾ HOẠCH TRƯỚC KHI HÀNH ĐỘNG (PLAN-FIRST RULE)

Mọi AI Agent khi nhận nhiệm vụ kỹ thuật tại Gateway **BẮT BUỘC PHẢI LẬP VÀ TRÌNH BÀY PLAN** trước khi thực hiện bất kỳ hành động sửa đổi mã nguồn, thêm/xóa file, refactor hay thay đổi cấu hình nào:
1. **Mục tiêu (Objective)**: Tóm tắt chính xác điều cần làm.
2. **Context & Files cần đọc**: Liệt kê các file nội bộ của Gateway theo ma trận đọc file tại Nguyên tắc 3.
3. **Phân tích rủi ro & Phạm vi ảnh hưởng (Impact Analysis)**.
4. **Checklist các bước thực hiện (Step-by-Step Implementation)**.
5. **Kế hoạch kiểm chứng (Verification)**: `dotnet build Gateway.sln -c Release` và `dotnet test Gateway.sln -c Release`.

---

## 🧭 NGUYÊN TẮC 2: QUY TẮC PHẠM VI NỘI BỘ (INWARD-ONLY / SELF-CONTAINED)

Gateway là một Git Repository / Submodule độc lập:
* **TUYỆT ĐỐI CHỈ ĐƯỢC trỏ tới các tài nguyên nội bộ của Gateway**.
* **TUYỆT ĐỐI KHÔNG ĐƯỢC trỏ ngược ra ngoài Root** (`../../../docs/...`) hay sang các Service khác (`../../Catalog/...`).
* Toàn bộ tài liệu, hướng dẫn và cấu hình trong repo này phải 100% tự cung tự cấp (Self-contained).

---

## 📚 NGUYÊN TẮC 3: DANH MỤC FILE NỘI BỘ BẮT BUỘC PHẢI ĐỌC TẠI GATEWAY

Khi bắt đầu làm việc tại Gateway, AI Agent **BẮT BUỘC PHẢI ĐỌC** các file nội bộ sau theo thứ tự:
1. `README.md`: Tổng quan kiến trúc YARP Gateway, pipeline middleware, cấu hình môi trường.
2. `specs/01_ARCHITECTURE.md`: Kiến trúc pipeline, Response Envelope standard, Identity Propagation, Redis Rate Limiting.
3. `specs/02_ROUTING_CATALOG.md`: Danh mục định tuyến Reverse Proxy tới 9 cụm microservices.
4. `docs/HOW_TO_GUIDE.md`: Hướng dẫn thêm route mới, middleware và kiểm thử.
5. `docs/EXPLANATION_ARCHITECTURE.md`: Giải thích chi tiết các quyết định thiết kế kiến trúc.

---

## 🔒 NGUYÊN TẮC 4: RÀO CHẮN BẤT BIẾN TẠI GATEWAY

1. **PORT 8080 LÀ BẤT BIẾN**: Gateway chạy cố định tại cổng HTTP `8080` (tránh xung đột port 8000 của Portainer trên máy dev).
2. **GATEWAY KHÔNG CHỨA DATABASE HOẶC STATEFUL DOMAIN LOGIC**: Gateway là Edge Reverse Proxy phi trạng thái (Stateless). Tuyệt đối không tạo `DbContext`, `DbSet<T>`, hoặc kết nối cơ sở dữ liệu.
3. **QUY CHUẨN PHÂN TRANG (ZERO PAGINATION CALCULATION AT GATEWAY)**:
   - Gateway tuyệt đối **KHÔNG** thực hiện tính toán phân trang, không nhận tập dữ liệu thô lớn không giới hạn về bộ nhớ để tự cắt trang (gây nghẽn mạng và sập bộ nhớ gateway).
   - Phân trang tính toán 100% tại từng microservice nghiệp vụ (`OFFSET/LIMIT` và `COUNT(*)`).
   - Gateway chỉ chuyển tiếp hoặc tự động bọc payload phân trang từ service thành envelope `ApiResponse<PagedData<T>>`.
4. **BẢO VỆ DANH TÍNH CHỐNG GIẢ MẠO CHỦ ĐỘNG (ACTIVE ANTI-SPOOFING & IDENTITY PROPAGATION)**:
   - `ClientContextMiddleware` chủ động kiểm tra (case-insensitive) mọi header định danh nhạy cảm gửi từ client bên ngoài (`x-user-id`, `x-user-email`, `x-user-roles`, `x-user-role`, `x-user-permissions`, `x-user-permission`, `x-internal-service-key`).
   - Nếu phát hiện bất kỳ header nào trong danh sách cấm, Gateway lập tức ngắt pipeline, ghi log cảnh báo bảo mật kèm Client IP và trả về HTTP `403 Forbidden` (`errorCode: "FORBIDDEN_HEADER_SPOOFING"`, `message: "Security violation: Untrusted identity header detected in external request."`).
   - Chỉ các header do Gateway tự giải mã và xác thực từ JWT Bearer token hợp lệ mới được tiêm an toàn vào downstream microservices qua `IdentityPropagationTransform` (`x-user-id`, `x-user-email`, `x-user-roles`, `x-user-permissions`).
5. **HẠ TẦNG REDIS DÙNG CHUNG (SHARED REDIS) — PORT 6379**:
   - Gateway kết nối tới Redis dùng chung (`REDIS_CONNECTION_STRING`) để thực hiện Rate Limiting trên các route nhạy cảm (OTP, Auth).
   - Tuyệt đối cấm khai báo container Redis riêng biệt bên trong submodule Gateway.
6. **CẤM COMMIT SECRET**: Mọi secret key (JWT Signature, Connection String) phải được nạp qua biến môi trường.
7. **QUY CHUẨN ĐỊNH DANH ROUTE 100% SỐ NHIỀU (PLURAL ROUTE STANDARD)**:
   - Gateway chỉ định tuyến các route collections số nhiều: `/api/v1/users`, `/api/v1/products`, `/api/v1/categories`, `/api/v1/carts`, `/api/v1/orders`, `/api/v1/shipments`, `/api/v1/auctions`, `/api/v1/wallets`, `/api/v1/notifications`, `/api/v1/logs`.
   - Các route quản trị admin phải phân tách rõ ràng theo domain: `/api/v1/admin/users/**`, `/api/v1/admin/auctions/**`, `/api/v1/admin/wallets/**`, `/api/v1/admin/notifications/**`. CẤM dùng 1 route duy nhất `admin-route` nuốt toàn bộ path sang UserService.
   - Ngoại lệ đơn lẻ: `/api/v1/me` và `/api/v1/auth`.
8. **INTERNAL API ĐANG TẠM KHÓA (2026-10-31)**:
   - `ClientContextMiddleware` vẫn bóc tách (remove) header `X-Internal-Service-Key` từ mọi request bên ngoài để chống giả mạo danh tính nội bộ.
   - Khối chặn path `/api/v1/internal` và `/internal/` đang bị comment. Request đi qua như API thường, không trả 404.
   - Bật lại khối chặn cần phê duyệt rõ ràng của Người dùng và cập nhật đồng bộ test, docs.
   - TUYỆT ĐỐI CẤM khai báo bất kỳ YARP route nào cho các endpoint `/internal/*` trong `appsettings.json`.

---

## 💬 NGUYÊN TẮC 5: QUY CHUẨN THÔNG BÁO LỖI TOÀN HỆ THỐNG (MANDATORY ERROR & ENUM RULES)

1. **Ngôn ngữ (100% English)**: Toàn bộ thông báo lỗi viết bằng 100% tiếng Anh. Tuyệt đối CẤM dùng tiếng Việt trong response và runtime exception.
2. **Quy cách viết hoa thông báo lỗi (100% Sentence case)**:
   - Viết hoa DUY NHẤT chữ cái đầu tiên của câu thông báo.
   - Toàn bộ các từ tiếp theo viết thường, TRỪ các từ viết tắt kỹ thuật hoặc tên riêng: `ID`, `OTP`, `JWT`, `URL`, `UUID`, `Email`, `API`, `HTTP`.
   - TUYỆT ĐỐI CẤM viết hoa toàn bộ thông báo lỗi (ví dụ cấm: `"UNAUTHORIZED"`, `"TOO MANY REQUESTS"`).
   - Ví dụ đúng: `"Too many requests. Please try again later."`, `"Authentication is required to access administrative endpoints."`
3. **Mã lỗi (100% SCREAMING_SNAKE_CASE)**:
   - Viết hoa toàn bộ và nối bằng gạch dưới: `UNAUTHORIZED`, `FORBIDDEN`, `RATE_LIMIT_EXCEEDED`, `SERVICE_UNAVAILABLE`, `VAL_INVALID_INPUT`, `INTERNAL_SERVER_ERROR`.
   - CẤM tiền tố `ERR_`. Tiền tố `VAL_` chỉ dành riêng cho lỗi validation đầu vào.
4. **Quy chuẩn Tuần tự hóa Enum đầu ra REST API (PascalCase Serialization)**:
   - Toàn bộ enum trả về trong JSON envelope phải là chuỗi `PascalCase` (`"Active"`, `"Pending"`), cấm chuỗi in hoa toàn bộ hoặc số nguyên.
5. **Cơ chế Duy trì Rule Bền vững ("Ghi vào Rule")**:
   - Khi Người dùng (Sếp) chỉ thị "ghi vào rule", AI Agent bắt buộc phải lập tức ghi nhận và đồng bộ vào file này, `.agents/AGENTS.md`, root `AGENTS.md` và `.cursor/rules/`.

---

## 🎯 NGUYÊN TẮC 6: ĐIỀU KIỆN HOÀN THÀNH NHIỆM VỤ (DEFINITION OF DONE)

Một task tại Gateway chỉ hoàn tất khi:
* Đã lập Plan chi tiết trước khi thực hiện.
* Không chứa bất kỳ liên kết trỏ ngược ra ngoài Gateway (100% Inward-Only).
* `dotnet build Gateway.sln -c Release` thành công 100% (0 error).
* `dotnet test Gateway.sln -c Release` vượt qua 100% các bài test trong `Gateway.Tests`.
* Trạng thái Git sạch, không commit file rác (`bin/`, `obj/`, `.vs/`, `.idea/`, `.env`).
