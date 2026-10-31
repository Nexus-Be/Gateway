# Giải Thích Kiến Trúc Và Các Cơ Chế Xử Lý Của Gateway (Gateway Architecture & Processing Mechanisms)

Tài liệu này giải thích chi tiết các quyết định thiết kế, nguyên lý vận hành và toàn bộ các cơ chế xử lý lưu lượng mạng tại Edge API Gateway của hệ sinh thái Nexus.

---

## 1. Vị Trí Kiến Trúc Và Vai Trò Cốt Lõi

API Gateway đóng vai trò là điểm tiếp nhận lưu lượng mạng duy nhất (Single Entrypoint) tại cổng `8080` cho toàn bộ các ứng dụng khách bên ngoài (Web Next.js, Mobile App, Postman). 

Mọi yêu cầu gửi từ mạng công cộng không được phép đi trực tiếp vào các microservice nội bộ. Gateway tạo thành vành đai phòng vệ biên (Edge Security Boundary) với 5 nhiệm vụ trọng tâm:
1. Tiếp nhận và định tuyến luồng HTTP trực tiếp tới 9 microservices backend (`8001`-`8009`).
2. Xác thực tính hợp lệ của token JWT Bearer và bóc tách claims thành các HTTP header tin cậy.
3. Chặn đứng các nỗ lực giả mạo danh tính và các truy cập trái phép vào tài nguyên nội bộ.
4. Bảo vệ hệ thống trước các đợt tấn công vét cạn bằng bộ khống chế tần suất phân tán qua Redis.
5. Tự động đóng gói dữ liệu phản hồi theo phong bì chuẩn hóa `ApiResponse<T>` trước khi trả về cho trình duyệt.

Gateway hoạt động theo mô hình phi trạng thái (Stateless). Gateway không kết nối cơ sở dữ liệu quan hệ, không chứa thực thể nghiệp vụ và không thực hiện tính toán miền (Domain Logic).

---

## 2. Vì Sao Sử Dụng Microsoft YARP Thay Vì Controller Chuyển Tiếp?

Trong các kiến trúc Gateway cũ, lập trình viên thường dùng Controller để tiếp nhận request, parse thành đối tượng C# DTO, rồi dùng `HttpClient` gọi tiếp sang backend. Phương án này bộc lộ ba điểm nghẽn nghiêm trọng khi lưu lượng tăng cao:

### Áp lực bộ nhớ và bộ thu gom rác (Garbage Collection Pressure)
Khi dùng Controller thông thường, toàn bộ payload JSON phải được nạp đầy đủ vào bộ nhớ RAM (Heap Memory) để chuyển thành object C#, sau đó tuần tự hóa ngược lại thành JSON. Quá trình này tạo ra hàng triệu đối tượng tạm thời trên bộ nhớ, buộc bộ thu gom rác (GC) của .NET phải tạm dừng luồng xử lý (GC Pause) để dọn dẹp, làm suy giảm đột ngột độ trễ mạng (Latency Spike).

Microsoft YARP (Yet Another Reverse Proxy) sử dụng cơ chế truyền phát trực tiếp (Direct HTTP Streaming) qua thư viện `System.IO.Pipelines`. Dữ liệu từ socket mạng của client được chuyển tiếp thẳng sang socket mạng của backend microservice mà không phân tích cú pháp toàn bộ nội dung JSON. Cơ chế này đạt mức phân bổ bộ nhớ xấp xỉ bằng không (Zero-allocation), giúp Gateway duy trì hiệu năng ổn định dưới tải hàng chục nghìn request mỗi giây.

### Quản lý nhóm kết nối mạng (Socket Connection Pooling)
Việc khởi tạo thủ công `HttpClient` hoặc tạo mới kết nối TCP cho mỗi request dẫn đến cạn kiệt cổng socket (Socket Exhaustion). YARP tích hợp sẵn cơ chế quản lý nhóm kết nối HTTP/1.1 và HTTP/2 thông minh, tái sử dụng các kết nối TCP đang mở sẵn tới các backend service, loại bỏ hoàn toàn độ trễ bắt tay mạng (TCP/TLS Handshake).

### Chi phí bảo trì mã nguồn (Maintenance Overhead)
Nếu dùng Controller làm proxy trung gian, mỗi khi một microservice backend bổ sung một trường dữ liệu mới hoặc thêm một API mới, lập trình viên bắt buộc phải cập nhật DTO và viết thêm action trên Gateway. Sử dụng YARP giúp cấu hình định tuyến được điều khiển hoàn toàn qua file khai báo JSON (`appsettings.json`), giải phóng đội ngũ kỹ sư khỏi việc viết code chuyển tiếp thủ công.

---

## 3. Thứ Tự Phân Lớp Trong Đường Ống Xử Lý (Middleware Pipeline Sequence)

Mọi yêu cầu mạng đi qua Gateway đều traverse qua một đường ống phân cấp nghiêm ngặt trước khi chạm tới cụm dịch vụ đích:

```text
Yêu cầu HTTP từ Client (Port 8080)
  │
  ▼
[ 1. GlobalExceptionMiddleware ]   --> Bắt toàn bộ lỗi chưa được xử lý thành ApiErrorResponse
  │
  ▼
[ 2. ClientContextMiddleware ]     --> Active Anti-Spoofing (403), Gán IP/TraceId, tạm hoãn chặn /internal/*
  │
  ▼
[ 3. UseCors ("AllowFrontend") ]   --> Xử lý yêu cầu CORS Preflight (OPTIONS)
  │
  ▼
[ 4. UseAuthentication & Authz ]   --> Xác thực chữ ký số JWT Bearer và nạp ClaimsPrincipal
  │
  ▼
[ 5. SensitiveRateLimitMiddleware]--> Khống chế tần suất OTP, Register, Login qua Redis
  │
  ▼
[ 6. AdminAuthorizationGuard ]     --> Kiểm tra vai trò Admin/Staff trên các route /api/v1/admin/**
  │
  ▼
[ 7. IdempotencyGuardMiddleware ]  --> Kiểm tra tính hợp lệ của header Idempotency-Key
  │
  ▼
[ 8. ResponseEnvelopeMiddleware ]  --> Đệm luồng phản hồi 200/201 và bọc tự động vào ApiResponse<T>
  │
  ▼
[ 9. YARP ReverseProxy Engine ]    --> Kích hoạt IdentityPropagationTransform và stream tới Microservice
```

Thứ tự sắp xếp này tuân thủ nguyên tắc loại trừ sớm:
- Các lỗi hệ thống và vi phạm bảo mật giả mạo header được chặn đứng ngay tại hai lớp đầu tiên (`GlobalExceptionMiddleware` và `ClientContextMiddleware`) để không lãng phí CPU cho việc giải mã token JWT hay kiểm tra chính sách CORS.
- Lớp kiểm tra tần suất (`SensitiveRateLimitMiddleware`) được đặt trước khi điều phối tới backend để dập tắt các cuộc tấn công vét cạn ngay tại cửa ngõ.

---

## 4. Cơ Chế Chặn Đứng Giả Mạo Danh Tính Chủ Động (Active Anti-Header-Spoofing Rejection)

Trong môi trường mạng không tin cậy, kẻ tấn công có thể cố tình gửi các header định danh như `x-user-id: <admin-guid>` hoặc `x-user-roles: admin` nhằm chiếm đoạt quyền hạn của người khác nếu các dịch vụ backend đọc header này một cách thiếu kiểm chứng.

### So sánh phương án xử lý
Hệ thống lựa chọn phương án **Chặn Đứng Chủ Động (Active Rejection)** thay vì phương án Xóa Bỏ Im Lặng (Silent Stripping):

| Tiêu chí | Phương án A: Xóa bỏ im lặng (Silent Strip) | Phương án B: Chặn đứng chủ động (Active Rejection - Được chọn) |
|:---|:---|:---|
| **Hành vi xử lý** | Gateway âm thầm xóa header vi phạm rồi chuyển tiếp request đi tiếp. | Gateway lập tức ngắt đường ống, trả về HTTP 403 và mã lỗi `FORBIDDEN_HEADER_SPOOFING`. |
| **Khả năng quan sát** | Kém. Kẻ tấn công có thể liên tục thử nghiệm mà hệ thống giám sát không phát hiện. | Minh bạch. Hệ thống ghi log cảnh báo mức Warning kèm Client IP, TraceId và đường dẫn bị tấn công. |
| **Độ an toàn** | Tiềm ẩn rủi ro nếu thứ tự middleware bị cấu hình sai lệch khiến header lọt vào trong. | An toàn tuyệt đối. Yêu cầu độc hại bị triệt tiêu ngay tại tầng đầu tiên của Gateway. |
| **Tính răn đe** | Không có tín hiệu phản hồi cho client. | Rõ ràng. Phản hồi lỗi vi phạm bảo mật trực tiếp cho client. |

### Cách thức thực thi trong mã nguồn
Tại `Gateway/Middlewares/Global/ClientContextMiddleware.cs`, danh sách 7 header cấm kỵ được kiểm tra không phân biệt chữ hoa hay chữ thường:
```csharp
private static readonly string[] ForbiddenSpoofedHeaders = new[]
{
    "x-user-id",
    "x-user-email",
    "x-user-roles",
    "x-user-role",
    "x-user-permissions",
    "x-user-permission",
    "x-internal-service-key"
};
```
Nếu phát hiện bất kỳ header nào trong danh sách trên từ yêu cầu bên ngoài, Gateway lập tức trả về HTTP `403 Forbidden`:
```json
{
  "success": false,
  "statusCode": 403,
  "errorCode": "FORBIDDEN_HEADER_SPOOFING",
  "message": "Security violation: Untrusted identity header detected in external request.",
  "traceId": "0HNPGT7G285Q2",
  "timestamp": "2026-10-31T01:30:00Z"
}
```

---

## 5. Trạng Thái Tạm Hoãn Cách Ly Các API Nội Bộ (Internal Route Block Postponed)

Hệ thống Nexus từng phân định Public REST API (phục vụ người dùng qua Gateway) và Internal REST API (chỉ giao tiếp giữa các microservice). Từ 2026-10-31, mô hình Internal API tạm khóa.

Các route liên service chuyển sang tiền tố công khai và đi qua Gateway như API thường, gồm nạp lô nhật ký (`POST /api/v1/logs/batch`), gửi OTP email (`POST /api/v1/notifications/email/otp`), và thanh toán trực tiếp từ ví (`POST /api/v1/wallets/checkout/direct`).

### Trạng thái hiện tại:
1. **Khối chặn tại Middleware đã bị comment:** `ClientContextMiddleware` không còn từ chối path `/api/v1/internal` hay `/internal/`, nên request đi tiếp vào pipeline như API thường.
2. **Quy chuẩn cấu hình YARP vẫn giữ:** Trong file `Gateway/appsettings.json`, tuyệt đối cấm khai báo bất kỳ route nào có đường dẫn chứa `/internal/`.
3. **Bật lại cần phê duyệt:** Khôi phục khối chặn phải có phê duyệt rõ ràng của Người dùng và cập nhật đồng bộ middleware, test, và tài liệu.

---

## 6. Cơ Chế Lan Truyền Danh Tính Tin Cậy (Identity Propagation Transform)

Gateway áp dụng mô hình xác thực tại biên (Edge Authentication & Claim Forwarding).

Client gửi token JWT qua header chuẩn `Authorization: Bearer <token>`. Gateway giải mã token, xác minh chữ ký số đối với khóa bí mật `Jwt:Signature`, và kiểm tra hạn dùng của token.

Khi chuyển tiếp yêu cầu tới microservice đích, `Gateway/Transforms/IdentityPropagationTransform.cs` trích xuất thông tin định danh và tiêm vào request downstream dưới dạng HTTP header:
- `x-user-id`: GUID định danh duy nhất của người dùng (trích xuất từ claim `sub`, `userId`, hoặc `id`).
- `x-user-email`: Email của người dùng (trích xuất từ claim `email`).
- `x-user-roles`: Chuỗi các vai trò viết thường, nối với nhau bằng dấu phẩy (ví dụ: `buyer,seller`).
- `x-user-permissions`: Chuỗi các quyền hạn viết hoa, nối với nhau bằng dấu phẩy (ví dụ: `ORDER_CREATE,PRODUCT_MANAGE`).
- `x-client-ip`: Địa chỉ IP thực tế của client (sau khi đã bóc tách từ kết nối mạng hoặc header `X-Forwarded-For`).
- `x-trace-id`: Mã định danh truy vết phân tán theo chuẩn W3C để đồng bộ log qua Tempo và Grafana.

### Lợi ích kiến trúc
Các microservice nội bộ hoàn toàn tin tưởng các header `x-user-*` này vì mọi header giả mạo từ bên ngoài đã bị Gateway chặn đứng từ trước. Các service chỉ cần đọc dữ liệu qua `HttpContext.Request.Headers` mà không phải tốn chu kỳ CPU để giải mã lại chữ ký số JWT hay kết nối tới máy chủ cấp quyền trung tâm trên từng request.

---

## 7. Cơ Chế Tự Động Đóng Gói Phong Bì Dữ Liệu (Automatic Response Envelope)

Quy chuẩn kiến trúc Nexus quy định toàn bộ phản hồi thành công trả về cho Frontend đều phải nằm trong một phong bì JSON chuẩn:
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Operation completed successfully.",
  "data": { ... },
  "traceId": "0HNPGT7G285Q2",
  "timestamp": "2026-10-31T01:30:00Z"
}
```

Thay vì bắt buộc từng lập trình viên phải tự bọc đối tượng này trên hàng trăm controller của 9 microservice, Gateway thực hiện việc này một cách tự động và trong suốt tại `Gateway/Middlewares/Global/ResponseEnvelopeMiddleware.cs`.

### Nguyên lý hoạt động của `ResponseEnvelopeMiddleware`:
1. **Đệm luồng dữ liệu (Stream Buffering):** Middleware tráo đổi luồng `context.Response.Body` bằng một luồng `MemoryStream` tạm thời để hứng dữ liệu do microservice trả về.
2. **Kiểm tra điều kiện:**
   - Nếu mã trạng thái là HTTP `204 NoContent`, dữ liệu rỗng, hoặc mã lỗi ($\ge 300$), middleware bỏ qua và sao chép trực tiếp ra luồng gốc.
   - Nếu `Content-Type` không phải là `application/json` (ví dụ: tệp tin tải về, hình ảnh), middleware giữ nguyên luồng byte gốc.
3. **Phân tích cú pháp và bọc dữ liệu:**
   - Middleware đọc nội dung JSON từ luồng đệm qua `JsonDocument`.
   - Nếu đối tượng JSON đã có sẵn thuộc tính `success` (đã được bọc từ trước), middleware cho phép đi thẳng nhằm tránh bọc lồng nhau (Double-wrapping).
   - Nếu là dữ liệu thô (Raw DTO hoặc mảng JSON), middleware khởi tạo đối tượng `ApiResponse<JsonElement>`, gán `statusCode`, `traceId`, `timestamp`, gán dữ liệu thô vào trường `data`, sau đó tuần tự hóa và ghi trực tiếp ra socket mạng của client.

---

## 8. Cơ Chế Khống Chế Tần Suất Nhạy Cảm (Sensitive Route Rate Limiting)

Các chức năng gửi mã OTP, xác thực mã OTP, đăng ký tài khoản và đăng nhập là mục tiêu hàng đầu của các cuộc tấn công vét cạn mật khẩu hoặc làm cạn kiệt tài khoản gửi email qua dịch vụ SMTP.

Nếu để các yêu cầu này đi sâu vào `UserService`, hệ thống sẽ phải chịu tải truy vấn cơ sở dữ liệu và mã hóa mật khẩu bằng thuật toán BCrypt (vốn tiêu tốn nhiều tài nguyên CPU).

`Gateway/Middlewares/Features/SensitiveRateLimitMiddleware.cs` chặn đứng các yêu cầu vượt ngưỡng ngay tại Gateway:
- `POST /api/v1/auth/otp/resend`: Tối đa 3 lần trong 60 giây.
- `POST /api/v1/auth/otp/verify`: Tối đa 5 lần trong 60 giây.
- `POST /api/v1/auth/register`: Tối đa 5 lần trong 300 giây.
- `POST /api/v1/auth/login`: Tối đa 10 lần trong 60 giây.

### Thuật toán Fixed-Window qua Redis phân tán
Middleware xây dựng khóa định danh trên Redis theo cấu trúc:
```text
nexus:gateway:rate:strict:{METHOD}:{PATH}:{CLIENT_IP}:{WINDOW_TIMESTAMP}
```
Lệnh tăng số nguyên tử `StringIncrementAsync` (`INCR`) được thực hiện trên Redis với độ trễ dưới 1 mili giây. Khi bộ đếm vượt quá ngưỡng cho phép, Gateway lập tức trả về HTTP `429 Too Many Requests` kèm header `Retry-After`:
```json
{
  "success": false,
  "statusCode": 429,
  "errorCode": "RATE_LIMIT_EXCEEDED",
  "message": "Too many requests. Please slow down and try again later.",
  "traceId": "0HNPGT7G285Q2",
  "timestamp": "2026-10-31T01:30:00Z"
}
```

### Cơ chế dự phòng trong bộ nhớ (In-Memory Fallback)
Nếu kết nối tới Redis bị gián đoạn, middleware tự động chuyển đổi sang bộ nhớ RAM cục bộ thông qua cấu trúc dữ liệu luồng an toàn `ConcurrentDictionary<string, (long Count, long Window)>`. Cơ chế này bảo đảm Gateway không bao giờ làm gián đoạn lưu lượng của người dùng khi hạ tầng phụ trợ gặp sự cố.

---

## 9. Phân Quyền Hai Lớp: Phân Quyền Thô Tại Biên vs Phân Quyền Tinh Tại Dịch Vụ

Hệ thống Nexus áp dụng mô hình phân tách trách nhiệm phân quyền rõ ràng:

### Phân quyền thô tại biên (Coarse-Grained Authorization at Gateway)
Do `AdminAuthorizationGuardMiddleware.cs` đảm nhiệm. Middleware kiểm tra toàn bộ các yêu cầu có đường dẫn bắt đầu bằng `/api/v1/admin/**`:
- Nếu client chưa đăng nhập: Trả về HTTP `401 Unauthorized` (`UNAUTHORIZED`).
- Nếu client không chứa một trong các vai trò `admin`, `staff`, hoặc `administrator`: Trả về HTTP `403 Forbidden` (`FORBIDDEN`).

Gateway chỉ kiểm tra vai trò chung mà không cần truy vấn cơ sở dữ liệu.

### Phân quyền tinh tại dịch vụ (Fine-Grained Authorization at Microservices)
Do từng microservice nghiệp vụ phụ trách. Microservice đọc `x-user-id` và `x-user-roles` do Gateway tiêm vào để kiểm tra quyền sở hữu tài nguyên (Resource Ownership). Ví dụ:
- Người mua chỉ được xem đơn hàng và giỏ hàng của chính mình (`order.BuyerId == currentUserId`).
- Người bán chỉ được cập nhật sản phẩm thuộc gian hàng của mình (`product.SellerId == currentUserId`).

Việc phân tách này giúp Gateway giữ được tính chất mỏng nhẹ, trong khi các microservice nắm toàn quyền kiểm soát tính toàn vẹn của dữ liệu miền.

---

## 10. Quy Chuẩn Phân Trang Phi Trạng Thái (Zero Pagination Calculation at Gateway)

Gateway tuyệt đối **không** tham gia vào quá trình tính toán phân trang:
1. **Trách nhiệm của Database và Microservice:** Cơ sở dữ liệu PostgreSQL là nơi duy nhất thực hiện việc cắt trang thông qua các câu lệnh SQL tối ưu (`.Skip((page - 1) * pageSize).Take(pageSize)`) và đếm tổng số bản ghi (`.CountAsync()`). Microservice đóng gói kết quả vào đối tượng chuẩn `PagedData<T>`:
   ```json
   {
     "items": [ ... ],
     "pagination": {
       "pageIndex": 1,
       "pageSize": 20,
       "totalCount": 100,
       "totalPages": 5,
       "hasPreviousPage": false,
       "hasNextPage": true
     }
   }
   ```
2. **Trách nhiệm của Gateway:** Gateway chỉ chuyển tiếp yêu cầu, tiếp nhận payload phân trang từ microservice và bọc vào trường `data` của `ApiResponse<PagedData<T>>`. Gateway không bao giờ nhận tập dữ liệu thô lớn không giới hạn về bộ nhớ để tự cắt trang, loại bỏ triệt để nguy cơ tràn bộ nhớ RAM Gateway.

---

## 11. Kiểm Tra Tính Lũy Kế (Idempotency Key Guard)

Đối với các thao tác thay đổi trạng thái nhạy cảm (thanh toán đơn hàng, trừ tiền ví, đặt cọc đấu giá), các kết nối mạng chập chờn có thể khiến client gửi lại yêu cầu nhiều lần (Retry).

`Gateway/Middlewares/Features/IdempotencyGuardMiddleware.cs` kiểm tra sự hiện diện của header `Idempotency-Key` trên các phương thức `POST`, `PUT`, `PATCH`:
- Nếu header có giá trị nhưng rỗng hoặc có độ dài vượt quá 128 ký tự, Gateway từ chối ngay lập tức với HTTP `400 Bad Request` (`VAL_INVALID_INPUT`).
- Nếu hợp lệ, Gateway lưu tạm khóa này vào `HttpContext.Items` và `IdentityPropagationTransform` sẽ chuyển tiếp an toàn xuống dịch vụ downstream để các service như `Commerce` và `Wallet` lưu vết tránh trừ tiền hai lần.

---

## 12. Ma Trận Mã Lỗi RFC 9110 Toàn Diện Tại Gateway

Mọi phản hồi lỗi phát sinh từ chính Gateway đều được định dạng theo cấu trúc `ApiErrorResponse`, sử dụng 100% tiếng Anh, câu thông báo chuẩn Sentence case và mã lỗi viết hoa `SCREAMING_SNAKE_CASE`:

| HTTP Status | Error Code | Điều kiện phát sinh lỗi |
|:---|:---|:---|
| `400 BadRequest` | `VAL_INVALID_INPUT` | Header `Idempotency-Key` không hợp lệ hoặc dài vượt quá 128 ký tự. |
| `401 Unauthorized` | `UNAUTHORIZED` | Thiếu token JWT, token hết hạn, hoặc chữ ký số không hợp lệ trên route cần đăng nhập. |
| `403 Forbidden` | `FORBIDDEN_HEADER_SPOOFING` | Client gửi các header định danh bị cấm (`x-user-id`, `x-internal-service-key`...). |
| `403 Forbidden` | `FORBIDDEN` | Người dùng thiếu vai trò `admin` hoặc `staff` khi truy cập `/api/v1/admin/**`. |
| `404 NotFound` | `NOT_FOUND` | Route không tồn tại. Khối chặn `/api/v1/internal` đang tạm hoãn nên path nội bộ đi qua như API thường. |
| `429 TooManyRequests` | `RATE_LIMIT_EXCEEDED` | Vượt quá số lần gọi cho phép trong khung thời gian trên các tuyến xác thực nhạy cảm. |
| `500 InternalServerError` | `INTERNAL_SERVER_ERROR` | Lỗi ngoại lệ phát sinh không mong muốn trong quá trình thực thi middleware tại Gateway. |
| `503 ServiceUnavailable` | `SERVICE_UNAVAILABLE` | Cụm dịch vụ downstream tương ứng bị mất kết nối mạng hoặc không phản hồi. |
