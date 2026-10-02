# 🧪 DCExpense - Testing & Quality Assurance (QC) Report

## 1. Quality Strategy: Double-Layer Autonomous Verification

Quality Assurance in DCExpense adheres to an enterprise testing pyramid:
1. **Core Unit & Domain Tests (xUnit / FluentAssertions)**: Validates state machines, mathematical algorithms, idempotency, and database entity invariants.
2. **Integration Tests (ASP.NET Core WebApplicationFactory / EF Core)**: Validates end-to-end REST API pipelines, JWT middleware, Seed Data, and controller logic.
3. **Headless Browser E2E Tests (Playwright Automation)**: Validates real user interaction journeys on Google Chrome, including Single Sign-On (SSO), reactive DOM updates, modal forms, Chart.js canvases, and bank statement drag-and-drop file uploads.

---

## 2. Test Execution Summary

### 2.1. Backend Test Suite (.NET 8 xUnit)
Command:
```powershell
$env:DOTNET_ROOT = "C:\Users\noadv\AppData\Local\Microsoft\dotnet"; $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
dotnet test D:\Agy\DCExpense\DCExpense.sln
```

**Results:**
- Total Tests: **84 / 84 PASSED (100% GREEN)**
- Failures: 0
- Skipped: 0
- Execution Time: ~1.0 second

| Test Suite File | Count | Covered Scope |
|:---|:---:|:---|
| [`Phase1Tests.cs`](../DCExpense.Tests/Phase1Tests.cs) | 5 | Domain Invariants, GUID Keys, Parent-Child Splits, SHA-256 Fingerprint Generator |
| [`Phase2Tests.cs`](../DCExpense.Tests/Phase2Tests.cs) | 11 | Transaction State Machine, Sliding Window $\pm 7$d, Modulo 9 Transposition, CSV Parser |
| [`FrontendAndPwaTests.cs`](../DCExpense.Tests/FrontendAndPwaTests.cs) | 11 | `manifest.json` PWA schema, Icons, Service Worker precache, Index DOM layout |
| [`Phase4Tests.cs`](../DCExpense.Tests/Phase4Tests.cs) | 47 | Vietnamese Vernacular Currency Slang ("củ", "loét", "lít", "rưỡi", "tr", "k"), Telegram Webhook, CSPRNG Invite Codes (TTL 48h), RBAC 403 Forbidden |
| [`Phase5IntegrationTests.cs`](../DCExpense.Tests/Phase5IntegrationTests.cs) | 10 | Seed Data Ingestion, Net Worth & Safe-to-Spend Computation, Live DB CRUD, CSV Matcher |

---

### 2.2. Browser E2E Test Suite (Playwright)
Command:
```powershell
cd D:\Agy\DCExpense\tests-e2e
npx playwright test
```

**Results:**
- Total Scenarios: **5 / 5 PASSED (100% GREEN)**
- Engine: Google Chrome Headless Channel
- Execution Time: ~32.6s

| Scenario | Objective | Status | Evidence Artifact |
|:---:|:---|:---:|:---|
| **Test 1** | **Authentication & SSO Flow**: Opens SPA, clicks 1-Touch Quick Login, validates JWT acquisition, checks Header greeting "Triệu Quang Quân" and logout visibility. | **PASS** | `screenshots/test1_auth_sso_success.png` |
| **Test 2** | **Dynamic Database Data Binding**: Verifies live Net Worth (`134.820.000 đ`), confirms 4 real wallets, 50/30/20 budget progress bars, and Chart.js cash flow canvas. | **PASS** | `screenshots/test2_database_data_binding.png` |
| **Test 3** | **Real Transaction Creation**: Opens modal, selects live Accounts & Categories from DB, fills "Phở sáng Playwright E2E" 65.000 đ, saves to DB, verifies table update. | **PASS** | `screenshots/test3_transaction_creation.png` |
| **Test 4** | **Bank Reconciliation & 1-Touch Merge**: Navigates to Staging UI, uploads `statement_vcb_sample.csv`, checks 3 classified categories (Auto-merged, Suspect, New), executes 1-touch merge. | **PASS** | `screenshots/test4_bank_reconciliation.png` |
| **Test 5** | **Theme Toggle**: Toggles theme between Light and Dark mode, verifies `html.dark` class presence and localStorage persistence. | **PASS** | `screenshots/test5_theme_toggle.png` |

---

## 3. Playwright Photographic Evidence

Screenshots captured during test execution are stored in:
`D:\Agy\DCExpense\tests-e2e\screenshots/`
- `test1_auth_sso_success.png`
- `test2_database_data_binding.png`
- `test3_transaction_creation.png`
- `test4_bank_reconciliation.png`
- `test5_theme_toggle.png`
