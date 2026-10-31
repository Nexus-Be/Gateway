# Hướng Dẫn Vận Hành Và Phát Triển Gateway (How-to Guide)

Tài liệu này hướng dẫn bạn thực hiện các thao tác cấu hình, vận hành và mở rộng tính năng trên Gateway.

---

## 1. Khởi Chạy Gateway Cục Bộ

Để chạy Gateway trên máy phát triển, bạn thực hiện các bước sau.

### Bước 1: Khởi động Redis
Gateway sử dụng Redis cho bộ đếm Rate Limiting. Nếu Redis chưa chạy, Gateway tự động chuyển sang bộ nhớ RAM cục bộ. 
Để bật Redis qua Docker:
```bash
docker run -d --name nexus-redis -p 6379:6379 redis:alpine
```

### Bước 2: Cài đặt biến môi trường
Nếu bạn chạy trên môi trường Development, Gateway sử dụng khóa JWT mặc định trong file `Gateway/appsettings.json`. 
Để ghi đè khóa bảo mật qua biến môi trường:
```bash
export Jwt__Signature="YourProductionSecretKeyWithSufficientLength123456789!"
export Redis__ConnectionString="localhost:6379"
```

### Bước 3: Biên dịch và chạy Gateway
Chạy lệnh sau từ thư mục gốc của repository Gateway:
```bash
dotnet build Gateway.sln -c Release
dotnet run --project Gateway/Gateway.csproj
```
Gateway lắng nghe yêu cầu tại cổng `http://localhost:8080`.

### Bước 4: Mở Swagger UI
Ở môi trường Development, mở `http://localhost:8080` sẽ được chuyển tự động sang `http://localhost:8080/swagger`.
Trang này có dropdown gồm 10 mục: tài liệu của Gateway và 9 service downstream.
Chọn một service để xem hợp đồng của service đó, và bấm Try it out để gọi xuyên qua Gateway.

Khi Swagger bị tắt, `/` trả về JSON trạng thái thay vì chuyển hướng.
Để kiểm tra tự động toàn bộ trang Swagger của 10 ứng dụng, chạy từ thư mục gốc monorepo:
```bash
bash tools/verify-swagger.sh
```

---

## 2. Kiểm Tra Trạng Thái Hoạt Động (Health Check)

Để kiểm tra Gateway đã sẵn sàng tiếp nhận lưu lượng hay chưa, bạn gửi yêu cầu HTTP GET:
```bash
curl -i http://localhost:8080/health
```

Phản hồi thành công trả về HTTP 200:
```json
{
  "status": "Healthy",
  "service": "Nexus.Gateway",
  "timestamp": "2026-10-30T13:00:00Z"
}
```

---

## 3. Thêm Một Cụm Dịch Vụ Mới (Cluster)

Khi hệ thống có thêm microservice mới, bạn đăng ký địa chỉ mạng của dịch vụ vào file `Gateway/appsettings.json`.

Mở file `Gateway/appsettings.json`. Tìm khối `ReverseProxy:Clusters` và thêm cấu hình dịch vụ:

```json
"inventory-cluster": {
  "Destinations": {
    "inventory-service": {
      "Address": "http://localhost:8010"
    }
  }
}
```

---

## 4. Khai Báo Và Định Tuyến Route Mới

Sau khi khai báo Cluster, bạn thêm đúng một route cho mỗi tiền tố tài nguyên công khai.

Mở cả `Gateway/appsettings.json` và `Gateway/appsettings.example.json`, thêm cùng một mục vào khối `ReverseProxy:Routes`:

```json
"inventories-route": {
  "ClusterId": "inventory-cluster",
  "Match": {
    "Path": "/api/v1/inventories/{**catch-all}"
  }
}
```

Ba quy tắc khi thêm route.

1. Tên tài nguyên dùng số nhiều. Route số ít không được khai báo dưới bất kỳ hình thức nào.
2. Chỉ dùng một pattern `{**catch-all}` cho mỗi tiền tố. Catch-all khớp cả chuỗi rỗng nên cũng phủ chính đường dẫn gốc, không cần thêm route `-direct`.
3. Không thêm `Transforms` và `Order`. Mỗi tiền tố là duy nhất nên thứ tự không có tác dụng.

Sau khi sửa, cập nhật `Gateway.Tests/RouteConfigurationTests.cs` để danh sách route kỳ vọng khớp, rồi chạy `dotnet test Gateway.sln -c Release`.

---

## 5. Thêm Quy Tắc Rate Limit Riêng Cho Endpoint Nhạy Cảm

Để bảo vệ thêm một endpoint chống tấn công vét cạn hoặc gửi thư rác, bạn cập nhật bảng luật trong file `Gateway/Middlewares/Features/SensitiveRateLimitMiddleware.cs`.

Mở file `SensitiveRateLimitMiddleware.cs`. Thêm cặp phương thức HTTP và đường dẫn vào từ điển `SensitiveRules`. Khóa phải trùng chính xác với route thật, nếu không rule sẽ không bao giờ chạy:

```csharp
private static readonly Dictionary<string, RateLimitRule> SensitiveRules = new(StringComparer.OrdinalIgnoreCase)
{
    { "POST:/api/v1/auth/otp/resend",    new RateLimitRule(MaxRequests: 3, WindowSeconds: 60) },
    { "POST:/api/v1/auth/otp/verify",    new RateLimitRule(MaxRequests: 5, WindowSeconds: 60) },
    { "POST:/api/v1/auth/register",      new RateLimitRule(MaxRequests: 5, WindowSeconds: 300) },
    { "POST:/api/v1/auth/login",         new RateLimitRule(MaxRequests: 10, WindowSeconds: 60) },
    { "PUT:/api/v1/auth/password/reset", new RateLimitRule(MaxRequests: 3, WindowSeconds: 300) }
};
```

---

## 6. Chạy Kiểm Thử Tự Động

Để đảm bảo toàn bộ các bài kiểm thử đơn vị và tích hợp vượt qua kiểm tra trước khi tạo commit:
```bash
dotnet test Gateway.sln -c Release
```

Kết quả mong đợi: toàn bộ test đạt, `Failed: 0`.
