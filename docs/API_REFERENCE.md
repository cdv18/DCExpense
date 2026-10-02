# 📡 DCExpense - RESTful API Reference

Base URLs:
- **Local HTTP:** `http://localhost:5002`
- **Local HTTPS:** `https://localhost:7002`
- **Swagger UI:** `http://localhost:5002/swagger`

Authentication:
- All protected endpoints require a standard Bearer token in the `Authorization` header:
  `Authorization: Bearer <JWT_ACCESS_TOKEN>`

---

## 1. Authentication Endpoints

### `POST /api/auth/login`
Authenticates a user or returns a signed JWT token for DCManager SSO integration.

- **Request Body**:
  ```json
  {
    "email": "admin@dcmanager.local",
    "password": "Admin@123456"
  }
  ```
- **Response** (`200 OK`):
  ```json
  {
    "success": true,
    "data": {
      "accessToken": "eyJhbGciOiJIUzI1Ni...",
      "user": {
        "id": "718680ab-f7cb-41f2-b8c2-4fc9e2ad4594",
        "email": "admin@dcmanager.local",
        "fullName": "Triệu Quang Quân",
        "avatarUrl": "https://...",
        "roles": ["SuperAdmin"]
      }
    }
  }
  ```

---

## 2. Financial Dashboard Endpoints

### `GET /api/dashboard/summary`
Retrieves live aggregated Net Worth, Safe-to-Spend, Wallets, Budgets, and 5-month Cash Flow.

- **Security**: Bearer Token required (`[Authorize]`)
- **Response** (`200 OK`):
  ```json
  {
    "netWorth": 135000000.0,
    "totalAssets": 150000000.0,
    "totalLiabilities": 15000000.0,
    "safeToSpendToday": 120000.0,
    "accounts": [
      {
        "id": "22222222-2222-2222-2222-222222222221",
        "name": "Ví Tiền Mặt",
        "type": 0,
        "balance": 5000000.0,
        "isShared": true,
        "currency": "VND"
      },
      {
        "id": "22222222-2222-2222-2222-222222222222",
        "name": "Tài khoản Vietcombank",
        "type": 1,
        "balance": 120000000.0,
        "isShared": true,
        "currency": "VND"
      }
    ],
    "budgets": [
      {
        "name": "Nhu cầu thiết yếu (Needs - 50%)",
        "allocatedAmount": 15000000.0,
        "spentAmount": 6800000.0,
        "percentage": 45.33
      }
    ],
    "cashFlow": {
      "labels": ["T6", "T7", "T8", "T9", "T10"],
      "income": [28000000, 30000000, 32000000, 30000000, 0],
      "expense": [18000000, 21000000, 19500000, 16400000, 1840000]
    }
  }
  ```

---

## 3. Transactions & Splits

### `GET /api/transactions`
Retrieves paginated transactions with optional account, category, and date filtering.

### `POST /api/transactions`
Creates a new single transaction or a Parent transaction with multiple Child splits.

- **Request Body (Single Transaction)**:
  ```json
  {
    "accountId": "22222222-2222-2222-2222-222222222223",
    "categoryId": "33333333-3333-3333-3333-333333333331",
    "amount": 65000.0,
    "date": "2026-10-02T10:00:00Z",
    "note": "Ăn sáng phở bò",
    "type": 1
  }
  ```

- **Request Body (Split Transaction)**:
  ```json
  {
    "accountId": "22222222-2222-2222-2222-222222222222",
    "categoryId": "33333333-3333-3333-3333-333333333332",
    "amount": 500000.0,
    "date": "2026-10-02T10:00:00Z",
    "note": "Đi siêu thị tổng hợp",
    "type": 1,
    "splits": [
      {
        "categoryId": "33333333-3333-3333-3333-333333333331",
        "amount": 300000.0,
        "note": "Thực phẩm ăn uống"
      },
      {
        "categoryId": "33333333-3333-3333-3333-333333333332",
        "amount": 200000.0,
        "note": "Đồ gia dụng"
      }
    ]
  }
  ```

---

## 4. Bank Statement Reconciliation

### `POST /api/reconciliation/import`
Uploads a CSV statement file (`multipart/form-data`) and returns classified match results.

- **Form Fields**: `file` (File), `accountId` (UUID, optional).
- **Response** (`200 OK`):
  ```json
  {
    "autoMerged": [
      {
        "statement": { "date": "2026-10-01", "amount": 1200000, "description": "TIEN DIEN T9" },
        "matched": { "id": "...", "amount": 1200000, "note": "Thanh toán tiền điện" },
        "confidence": 0.98
      }
    ],
    "suspect": [
      {
        "statement": { "date": "2026-10-05", "amount": 450000, "description": "SHOPEEPAY" },
        "matched": { "id": "...", "date": "2026-10-02", "amount": 450000, "note": "Mua sắm Shopee" },
        "confidence": 0.72,
        "reason": "Lệch ngày (3 ngày) nhưng khớp số tiền và từ khóa"
      }
    ],
    "newTransactions": [
      { "date": "2026-10-06", "amount": 50000, "description": "HIGHLANDS COFFEE" }
    ]
  }
  ```

### `POST /api/reconciliation/merge`
Executes a 1-Touch Merge between a bank statement item and an existing internal record.

---

## 5. Household Multi-Tenancy

- `POST /api/households`: Create a new household.
- `GET /api/households/my`: Get all households the current user belongs to.
- `GET /api/households/{id}`: View household members and role permissions.
- `POST /api/households/{id}/invite-code`: Generate 6-char CSPRNG invite code with 48h TTL.
- `POST /api/households/join`: Join a household via invite code (`{ "inviteCode": "HD89XY" }`).
- `PUT /api/households/{id}/members/{userId}/role`: Update member role (Owner/Admin only).
- `GET /api/households/{id}/shared-wallets`: Query shared funds (`IsShared = true`).

---

## 6. Telegram Ingestion Bot

- `POST /api/telegram/webhook`: Webhook endpoint with `X-Telegram-Bot-Api-Secret-Token` validation.
- `GET /api/telegram/status`: Health check for Telegram integration.
