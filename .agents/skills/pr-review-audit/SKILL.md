---
name: pr-review-audit
description: Audit and review Pull Requests in Nexus YARP Gateway (port 8080). Validates route configuration, edge security, envelope encapsulation, and zero-controller invariants. Generates persistent 3-file audit reports in .agents/review/{PR_SLUG}/ with human-only logic sign-off checklists, enforcing format-only fixes upon user approval.
---

# Pull Request Review and Audit Skill (`pr-review-audit`) - Gateway

> **Scope:** Gateway repository only (`src/Services/Gateway/`). 100% Inward-Only. Never reference the root workspace or sibling services.
> **Bất biến Zero-Controller & Zero-Database:** Gateway là Edge Reverse Proxy phi trạng thái (cổng 8080), không chứa controller, không chứa database hay stateful domain logic.

This skill governs the standardized quality audit and review process for Pull Requests in Gateway.

---

## 1. Rào Chắn Kích Hoạt & Ràng Buộc Bất Biến

### 1.1 Chỉ Kích Hoạt Khi Review Pull Request (PR-Only)
* Kỹ năng này **CHỈ ĐƯỢC CHẠY** khi người dùng yêu cầu review Pull Request trong Gateway.
* Cấm tự ý chạy trong các tác vụ code thông thường.

### 1.2 Rào Chắn Kiểm Tra Nhánh Git (Checkout Preflight Verification)
1. Kiểm tra nhánh hiện tại: `git rev-parse --abbrev-ref HEAD` hoặc `git status`.
2. Nếu đang ở `dev` hoặc `main`: **DỪNG LẠI NGAY**. Yêu cầu người dùng checkout sang nhánh của PR trước khi review.

### 1.3 Quy Tắc Ký Duyệt Nghiệp Vụ Của Con Người (Human-Only Logic Sign-off)
* Agent **tuyệt đối không tự ý đánh dấu `[x]`** vào các checklist logic, routing và bảo mật.
* Toàn bộ checklist trong `03_LOGIC_BUSINESS_AUDIT.md` phải để trống `[ ]` để Reviewer / Con người trực tiếp kiểm tra và tích hoàn thành.

### 1.4 Rào Chắn Không Sửa Logic (Strict Format-Only Fix)
* Lỗi cấu hình route/middleware: Agent chỉ ghi chú `[NOTE - LOGIC ISSUE]` kèm vị trí `file:line` để tác giả PR tự sửa.
* Agent **tuyệt đối không tự sửa logic định tuyến**. Pha 2 chỉ sửa lỗi format/conventions (JSON formatting, naming) khi được người dùng duyệt.

### 1.5 Báo Cáo Không Bị Xóa & Không Commit Lên Git
* Báo cáo trong `.agents/review/` là hồ sơ kiểm định, không xóa sau khi sửa format.
* `.agents/review/` nằm trong `.gitignore` để không bị commit vào Git.

### 1.6 Báo Cáo Lưu Cục Bộ Nội Bộ Service (Local Service Invariant)
* Báo cáo review PR của Gateway **BẮT BUỘC** lưu tại `.agents/review/{PR_SLUG}/` nội bộ trong repository Gateway.
* **TUYỆT ĐỐI KHÔNG** tạo báo cáo ở Root repository (`Nexus-Be/.agents/review/`).

### 1.7 Quy Chuẩn Commit & Tiêu Đề PR (Zero Emoji / Icon Rule - Rule 18)
* Tiêu đề PR và toàn bộ commit messages **TUYỆT ĐỐI CẤM** chứa emoji hoặc icon (`✨`, `🐛`, `🔧`, `♻️`, `🧪`, `📝`, `⚡`, `👷`, `💄`, `🔒`, `🚧`...).
* Tuân thủ 100% Conventional Commits thuần túy bằng tiếng Anh: `<type>(<scope>): <subject>` hoặc `<type>: <subject>`.

---

## 2. Cấu Trúc Báo Cáo Tại Gateway

Lưu tại (nội bộ Gateway): `.agents/review/{PR_CODE}-{GIT_AUTHOR}-{COMMIT_SUMMARY_SLUG}/`
* `01_API_DOCS_AUDIT.md`: Kiểm tra tài liệu route trong `docs/api/Routes/` đối soát với `appsettings.json`.
* `02_CONTROLLER_AUDIT.md`: Kiểm tra bất biến Zero-Controller (không tạo controller mới), route số nhiều trong YARP config, phân tách route admin theo domain (`/api/v1/admin/users/**`, `/api/v1/admin/wallets/**`), không dùng 1 route admin gộp.
* `03_LOGIC_BUSINESS_AUDIT.md`: Kiểm tra Active Anti-Spoofing trong `ClientContextMiddleware` (chặn đứng `x-user-*` từ client ngoài, trả 403), kiểm tra Identity Propagation Transform, kiểm tra Response Envelope wrapping, kiểm tra không tính toán phân trang tại Gateway.

---

## 3. Quy Trình 2 Pha

1. **Pha 1 (Read-Only):** Quét diff `git diff $(git merge-base HEAD origin/dev)...HEAD --name-only`, kiểm tra quy chuẩn, tạo 3 file báo cáo với checklist trống `[ ]`, hiển thị tóm tắt.
2. **Pha 2 (Format-Only Fix):** Khi người dùng duyệt plan sửa format, Agent chỉ sửa format/conventions. Sửa xong cập nhật `[x]` cho mục format đó. Giữ nguyên ghi chú logic cho tác giả PR tự sửa.
