# 💰 DCExpense - Next-Gen Intelligent Personal & Household Financial Management (PFM 2.0)

[![.NET 8](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-336791.svg)](https://www.postgresql.org/)
[![Playwright E2E](https://img.shields.io/badge/Playwright-100%25%20PASS-2EAD33.svg)](https://playwright.dev/)
[![xUnit Tests](https://img.shields.io/badge/Tests-84%2F84%20PASS-brightgreen.svg)](https://xunit.net/)
[![PWA Ready](https://img.shields.io/badge/PWA-Ready-purple.svg)](https://developer.mozilla.org/en-US/docs/Web/Progressive_web_apps)

**DCExpense** là hệ sinh thái quản lý tài chính và chi tiêu thông minh thế hệ mới, giải quyết triệt để rào cản người dùng phải nhập liệu thủ công (**Zero-Friction Ingestion**), loại trừ 100% rủi ro ghi đúp giao dịch (**Smart Deduplication & Reconciliation**), và hỗ trợ mô hình gia đình đa người dùng (**Household Multi-Tenancy**) kết hợp Single Sign-On (**DCManager SSO**).

---

## 🌟 Tính Năng Nổi Bật (Key Features)

### 1. 🚀 Nhập Liệu Không Rào Cản (Zero-Friction Ingestion)
- **Telegram Voice & Text NLP Bot**: Gửi tin nhắn thoại (*"Đổ xăng 70 ngàn vcb"*) hoặc tin nhắn vắn tắt (*"Ăn trưa phở bò 45k momo"*), AI Copilot tự động bóc tách thực thể số tiền (k, nghìn, củ, lít, rưỡi), danh mục, ví và ngày tương đối.
- **Xác nhận 2 chiều (2-Way Confirmation)**: Bot phản hồi tin nhắn kèm Inline Keyboard `[✅ Xác nhận]` hoặc `[❌ Hủy]`.
- **Tiến trình State Machine Khép kín**: `Draft` (chưa ảnh hưởng số dư) $\to$ `Pending` $\to$ `Cleared` $\to$ `Reconciled` (số tiền bất biến).

### 2. 🧮 Bộ Đối Soát Sao Kê Thông Minh (Smart Reconciliation Engine)
- **Cửa sổ trượt $\pm 7$ ngày (`Sliding-Window Match`)**: Tự động nhận diện và gộp các giao dịch chi từ ngày 15, ngày 20 mới nhớ nhập Telegram, và ngày 30 nhập sao kê ngân hàng CSV.
- **Bộ kiểm tra lỗi gõ nhầm đảo số (`Transposition Error Checker`)**: Áp dụng định lý số học $|A - B| \equiv 0 \pmod 9$ để phát hiện người dùng gõ nhầm đảo vị trí 2 chữ số (VD: gõ nhầm 350.000 đ thay vì 530.000 đ).
- **Phòng chờ đối soát trực quan (Staging UI)**: Phân loại 3 nhóm rõ ràng: **Auto-merged** ($\ge 80\%$), **Nghi vấn (Suspect)** kèm nút **Gộp 1-chạm (1-Touch Merge)**, và **Giao dịch mới**.

### 3. 👨‍👩‍👧‍👦 Quỹ Chung Gia Đình & Phân Quyền Bảo Mật (Household Multi-Tenancy & RBAC)
- **Tách biệt Quỹ chung & Ví cá nhân**: Ví chung (`IsShared = true`) hiển thị công khai cho mọi thành viên cùng theo dõi và chi tiêu; Ví cá nhân (`IsShared = false`) hoàn toàn riêng tư.
- **Mã mời an toàn (Invite Code TTL 48h)**: Bộ sinh mã 6 ký tự mật mã khử ký tự dễ nhầm (`0, O, 1, I`), thời hạn 48 giờ.
- **Phân quyền 4 cấp (RBAC)**: `Owner`, `Admin`, `Member`, `Viewer` bảo vệ nghiêm ngặt chống can thiệp trái phép.

### 4. 📱 Web SPA Hiện Đại & Cài Đặt PWA (Progressive Web App)
- **Glassmorphism UI**: Thiết kế bằng Tailwind CSS, hỗ trợ chuyển đổi **Dark / Light Mode** mượt mà.
- **Bộ đếm Safe-to-Spend & Net Worth**: Tính toán số tiền chi an toàn trong ngày và tổng hợp tài sản ròng thời gian thực.
- **Biểu đồ Dòng tiền & Ngân sách**: Trực quan hóa quy tắc **50/30/20 & JARS** và biểu đồ Chart.js dòng tiền 5 tháng.
- **PWA Offline Ready**: Cài đặt ứng dụng 1-chạm lên màn hình Desktop/Mobile, khởi động tức thì qua Service Worker cache-first.

### 5. 🔐 Tích Hợp Xác Thực Tập Trung DCManager SSO
- Chuẩn giao thức **OAuth 2.1 / OIDC với PKCE**, cấp phát và xác thực JWT Bearer Token.

---

## 🏗️ Kiến Trúc Hệ Thống (Clean Architecture)

```
D:\Agy\DCExpense/
├── DCExpense.Domain/                  # Core Business Entities (Household, Account, Transaction...)
├── DCExpense.Application/             # DTOs, Interfaces, Logic Services (AI NLP, Reconciliation, Dashboard...)
├── DCExpense.Infrastructure/          # PostgreSQL EF Core 8 DbContext, SeedData, Telegram Bot, CSV Parser
├── DCExpense.WebApi/                  # Host ASP.NET Core 8 Web API, Controllers & Web SPA Frontend
│   └── wwwroot/                       # Static PWA: index.html, app.js, manifest.json, sw.js
├── DCExpense.Tests/                   # 84 Tests Unit & Integration (100% Pass)
└── tests-e2e/                         # 5 Tests Playwright Headless Chrome (100% Pass)
```

---

## 🚀 Hướng Dẫn Khởi Chạy Nhanh (Quick Start)

### Yêu Cầu Tiên Quyết (Prerequisites)
- .NET SDK 8.0+
- PostgreSQL 16 (chạy tại `localhost:5432`, user/pass: `postgres/postgres`)
- Trình duyệt Web (Chrome, Edge, Firefox, Safari)

### 1. Khởi Chạy Ứng Dụng với 1-Click Script
Mở PowerShell và chạy:
```powershell
cd D:\Agy\DCExpense
.\start-dcexpense.ps1
```
Ứng dụng sẽ tự động khởi tạo database PostgreSQL, nạp dữ liệu mẫu ban đầu và lắng nghe tại:
- **Web SPA & PWA Application:** [http://localhost:5002](http://localhost:5002)
- **Swagger REST API:** [http://localhost:5002/swagger](http://localhost:5002/swagger)
- **DCManager SSO Gateway:** [http://localhost:5001](http://localhost:5001) / [https://localhost:7001](https://localhost:7001)

### 2. Trải Nghiệm Trên Giao Diện
1. Truy cập [http://localhost:5002](http://localhost:5002).
2. Bấm nút: **`⚡ Đăng nhập nhanh Admin Demo (1-chạm)`** để đăng nhập với tài khoản quản trị `admin@dcmanager.local` (Triệu Quang Quân).
3. Màn hình Dashboard sống động hiển thị Net Worth `135.000.000 đ`, 4 ví thực tế và biểu đồ dòng tiền.
4. Bấm **"+ Ghi chép mới"** để tạo giao dịch hoặc vào **"Đối Soát Sao Kê"** để thử nghiệm kéo thả sao kê CSV.

---

## 🧪 Kiểm Thử Tự Động Hóa (Testing & Quality Assurance)

### Chạy Unit & Integration Tests (.NET xUnit)
```powershell
dotnet test D:\Agy\DCExpense\DCExpense.sln
```
> Kết quả: **84 / 84 Tests PASSED (100% GREEN)** trong ~1.0 giây.

### Chạy Playwright Browser End-to-End Tests
```powershell
cd D:\Agy\DCExpense\tests-e2e
npx playwright test
```
> Kết quả: **5 / 5 Scenarios PASSED (100% GREEN)** trên Google Chrome Headless.

---

## 📚 Tài Liệu Kỹ Thuật Chi Tiết (Technical Documents)
- 🏛️ [Kiến Trúc Hệ Thống & Sơ Đồ ERD](docs/ARCHITECTURE.md)
- 🧮 [Thuật Toán Đối Soát & Khử Trùng Lặp](docs/RECONCILIATION_ENGINE.md)
- 📡 [Đặc Tả Danh Mục API RESTful](docs/API_REFERENCE.md)
- 🧪 [Báo Cáo Kiểm Thử Tự Động & Bằng Chứng QC](docs/TESTING_AND_QC.md)

---
*Phát triển và bảo chứng chất lượng bởi Quy trình Tự Động Khép Kín (Autonomous Closed-Loop SDLC: Dev $\to$ QC Playwright $\to$ PM Sign-Off).*
