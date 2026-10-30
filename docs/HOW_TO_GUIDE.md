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

Sau khi khai báo Cluster, bạn cấu hình đường dẫn công khai để YARP chuyển tiếp yêu cầu đến service đích.

Mở file `Gateway/appsettings.json`. Trong khối `ReverseProxy:Routes`, thêm route mới:

```json
"inventory-route": {
  "ClusterId": "inventory-cluster",
  "Order": 110,
  "Match": {
    "Path": "/api/v1/inventory/{**catch-all}"
  }
}
```

### Ánh xạ lại đường dẫn (Path Transform)
Nếu đường dẫn công khai khác với đường dẫn nội bộ của service, bạn thêm phần `Transforms`. Ví dụ khi chuyển đổi `/api/v1/admin/*` thành `/api/v1/admin/user/*`:

```json
"admin-route": {
  "ClusterId": "user-cluster",
  "Order": 1,
  "Match": {
    "Path": "/api/v1/admin/{**catch-all}"
  },
  "Transforms": [
    {
      "PathPattern": "/api/v1/admin/user/{**catch-all}"
    }
  ]
}
```

---

## 5. Thêm Quy Tắc Rate Limit Riêng Cho Endpoint Nhạy Cảm

Để bảo vệ thêm một endpoint chống tấn công vét cạn hoặc gửi thư rác, bạn cập nhật bảng luật trong file `Gateway/Middlewares/Features/SensitiveRateLimitMiddleware.cs`.

Mở file `SensitiveRateLimitMiddleware.cs`. Thêm cặp phương thức HTTP và đường dẫn vào từ điển `SensitiveRules`:

```csharp
private static readonly Dictionary<string, RateLimitRule> SensitiveRules = new(StringComparer.OrdinalIgnoreCase)
{
    { "POST:/api/v1/auth/resend-otp", new RateLimitRule(MaxRequests: 3, WindowSeconds: 60) },
    { "POST:/api/v1/auth/verify-otp", new RateLimitRule(MaxRequests: 5, WindowSeconds: 60) },
    { "POST:/api/v1/auth/register",   new RateLimitRule(MaxRequests: 5, WindowSeconds: 300) },
    { "POST:/api/v1/auth/login",      new RateLimitRule(MaxRequests: 10, WindowSeconds: 60) },
    // Quy tắc mới cho tính năng đặt lại mật khẩu:
    { "POST:/api/v1/auth/reset-password", new RateLimitRule(MaxRequests: 3, WindowSeconds: 300) }
};
```

---

## 6. Chạy Kiểm Thử Tự Động

Để đảm bảo toàn bộ các bài kiểm thử đơn vị và tích hợp vượt qua kiểm tra trước khi tạo commit:
```bash
dotnet test Gateway.sln -c Release
```

Kết quả mong đợi:
```text
Passed!  - Failed: 0, Passed: 10, Skipped: 0, Total: 10
```
