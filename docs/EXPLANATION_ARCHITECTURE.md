# Giải Thích Kiến Trúc Gateway (Architecture Explanation)

Tài liệu này giải thích các quyết định kiến trúc và nguyên lý vận hành của API Gateway.

---

## 1. Vì Sao Dùng Microsoft YARP Thay Vì Controller Chuyển Tiếp Thủ Công?

Trong phiên bản trước, Gateway sử dụng Controller kế thừa `GatewayBaseController`. Cách tiếp cận này bộc lộ ba điểm nghẽn lớn khi hệ thống mở rộng.

Thứ nhất, chi phí bảo trì tăng cao. Mỗi khi một microservice thêm hoặc sửa endpoint, lập trình viên phải tạo DTO tương ứng và viết thêm action trên Gateway. Việc này gây lặp code và làm chậm tốc độ phát triển.

Thứ hai, lãng phí tài nguyên hệ thống. Khi dùng Controller thông thường, Gateway phải đọc toàn bộ payload HTTP, parse thành đối tượng C#, rồi lại tuần tự hóa ngược thành JSON để gửi đi. Quá trình này tiêu tốn nhiều bộ nhớ RAM và làm tăng tải cho bộ thu gom rác (Garbage Collector).

Thứ ba, Microsoft YARP giải quyết triệt để hai vấn đề trên bằng cơ chế streaming HTTP trực tiếp. YARP chuyển tiếp gói tin mạng ngay khi dữ liệu đến mà không cần phân tích toàn bộ nội dung. YARP quản lý sẵn nhóm kết nối (Connection Pooling), cân bằng tải và tự động phục hồi kết nối.

---

## 2. Vì Sao Tách Biệt Global Middleware Và Feature Middleware?

Đường ống xử lý yêu cầu (Request Pipeline) tại Gateway được chia thành hai lớp rõ ràng: lớp bao quát toàn cục (Global) và lớp chức năng chuyên biệt (Feature).

```text
Yêu cầu từ Client
  │
  ▼
[ Global Middlewares ]  --> Ngoại lệ, Client IP, Header Anti-Spoofing, CORS
  │
  ▼
[ Authentication ]      --> Xác thực tính hợp lệ của chữ ký JWT Bearer
  │
  ▼
[ Feature Middlewares ] --> Rate Limit tuyến nhạy cảm, Phân quyền Admin, Khóa Idempotency
  │
  ▼
[ YARP Engine ]         --> Gắn Claims vào Header, Chuyển tiếp HTTP stream tới Backend
  │
  ▼
[ Response Envelope ]   --> Đóng gói phản hồi 200/201 thô thành ApiResponse<T>
```

### Lớp Global Middlewares
Các middleware toàn cục luôn chạy trước với mọi yêu cầu. `GlobalExceptionMiddleware` bắt toàn bộ lỗi phát sinh từ mọi tầng để không làm lộ cấu trúc hệ thống. `ClientContextMiddleware` lập tức loại bỏ mọi header định danh giả mạo từ bên ngoài và ghi nhận IP thực của client.

### Lớp Feature Middlewares
Các middleware này chỉ kích hoạt khi yêu cầu thuộc về ngữ cảnh cụ thể. Việc tách riêng giúp logic từng phần cô lập hoàn toàn. Ví dụ `AdminAuthorizationGuardMiddleware` chỉ kiểm tra các route có tiền tố `/api/v1/admin`. Các yêu cầu xem sản phẩm công khai sẽ đi thẳng qua mà không tốn chi phí kiểm tra quyền hạn.

---

## 3. Vì Sao Đóng Gói Phong Bì ApiResponse Tại Gateway Thay Vì Ở Các Service?

Quy chuẩn hệ sinh thái Nexus yêu cầu mọi phản hồi gửi tới Frontend đều phải nằm trong phong bì chuẩn:
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Operation completed successfully.",
  "data": { ... },
  "traceId": "...",
  "timestamp": "2026-10-30T13:00:00Z"
}
```

Kiến trúc mới quyết định đặt `ResponseEnvelopeMiddleware` tại Gateway vì hai lý do cốt lõi.

Một là giữ cho 8 microservices bên trong hoàn toàn tập trung vào nghiệp vụ domain. Các service chỉ cần trả về DTO thô (`return Ok(profile);`). Chúng không cần phụ thuộc vào cấu trúc phong bì của Gateway.

Hai là tránh chỉnh sửa diện rộng. Nếu bắt từng microservice tự bọc dữ liệu, lập trình viên phải sửa đổi hàng trăm controller trên cả 8 kho mã nguồn độc lập. Đặt việc bọc dữ liệu tại Gateway giúp Frontend luôn nhận được định dạng đồng nhất, đồng thời giữ nguyên mã nguồn của toàn bộ các service nội bộ.

---

## 4. Cơ Chế Truyền Danh Tính (Identity Propagation) Qua HTTP Headers

Gateway hoạt động theo mô hình xác thực tại biên (Edge Authentication).

Client gửi token JWT một lần duy nhất qua header `Authorization: Bearer <token>`. Gateway giải mã token, kiểm tra chữ ký và trích xuất các thông tin định danh:
- `x-user-id`: Định danh duy nhất của người dùng dạng UUID.
- `x-user-email`: Địa chỉ email đã xác minh.
- `x-user-roles`: Danh sách quyền hạn viết thường, phân tách bằng dấu phẩy.
- `x-user-permissions`: Danh sách hành động được cấp phép viết hoa.
- `x-client-ip`: Địa chỉ IP thực tế của máy người dùng.

### Tính bảo mật chống giả mạo
Client bên ngoài không thể tự gửi header `x-user-id` để mạo danh người khác. `ClientContextMiddleware` luôn xóa sạch các header này ngay từ đầu vào. Chỉ những header do Gateway tự trích xuất từ JWT hợp lệ mới được gắn vào yêu cầu gửi xuống các microservice.

Các microservice bên trong mạng nội bộ chỉ cần đọc header chuỗi thông qua `HttpContextExtensions`. Nhờ đó, các service không phải thực hiện lại thao tác giải mã JWT tốn kém CPU.

---

## 5. Vì Sao Phải Giới Hạn Tần Suất (Rate Limiting) Nghiêm Ngặt Tại Biên?

Các chức năng gửi lại mã OTP, xác thực mã OTP, đăng ký tài khoản và đăng nhập là mục tiêu hàng đầu của các cuộc tấn công vét cạn hoặc làm cạn kiệt tài khoản gửi email của hệ thống.

Nếu để các yêu cầu này đi sâu vào `UserService`, hệ thống sẽ phải chịu tải truy vấn cơ sở dữ liệu và mã hóa mật khẩu bằng thuật toán BCrypt. Thuật toán này cố tình tiêu tốn nhiều CPU để tăng tính bảo mật.

`SensitiveRateLimitMiddleware` chặn đứng các yêu cầu vượt ngưỡng ngay tại Gateway bằng lệnh tăng số nguyên tử của Redis (`INCR`). Quá trình kiểm tra này diễn ra dưới 1 millisecond. Nhờ vậy, lưu lượng tấn công bị dập tắt trước khi kịp ảnh hưởng đến các service nội bộ.
