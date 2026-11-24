# Dashboard API and data contract

This contract describes the dashboard service boundary and the existing BAPStockManagement data that may support it. No model or migration changes are made by this feature. The EF implementation remains a skeleton until a backend owner confirms the data definitions and supplies missing business rules.

## Runtime API conventions

- Base path: `/api/v1`.
- Successful responses use `{ "data": ..., "meta": { "requestId": "...", "generatedAt": "UTC ISO-8601" } }`.
- JSON property names are camelCase. Enums use `UPPER_SNAKE_CASE`.
- Monetary values use `{ "amount": 123.45, "currency": "THB" }`.
- GET dashboard responses are client-cacheable for 30 seconds and support `ETag` / `If-None-Match`.
- Cookie-authenticated POST endpoints require the antiforgery token in `RequestVerificationToken` and an `Idempotency-Key`.
- Errors use `application/problem+json` with an RFC 7807 shape and `traceId`.
- OpenAPI is checked in as a manual contract in `openapi.json`; Swashbuckle was not added because the `.csproj` modification was not approved.

## Dashboard UI API wiring

- Product search debounces input and calls `GET /api/v1/products/search`; it only searches product names and SKUs.
- The What-if slider submits the selected SKU and quantity to `POST /api/v1/simulations/what-if` when the user clicks **ลองจำลอง**.
- Automatic purchase-request creation sends the currently displayed urgent-item SKUs to `POST /api/v1/purchase-requests/auto-generate`. The returned job message is shown so the mock's non-persistent behavior is explicit.
- POST requests include the page antiforgery token and a generated idempotency key. API errors are displayed next to the corresponding control.

## `IDashboardService` methods

### `GetSummaryAsync(CancellationToken)`

Returns KPIs, forecast points, stock-status slices, urgent items, AI insights, top products, and the initial What-if fixture.

Likely existing sources:

- `Products`: `ProductID`, `CategoryID`, `SKU`, `ProductName`, `IsActive`.
- `Categories`: `CategoryID`, `IsActive`.
- `ProductVariants`: `VariantID`, `ProductID`, `IsActive`.
- `StockBalances`: `VariantID`, `QtyPieces`, `QtyCases`, `LastUpdated`.
- `StockTransactions`: `VariantID`, `TransactionTypeID`, `TxnDate`, `QtyPieces`, `QtyCases`.
- `TransactionTypes`: `TransactionTypeID`, `Direction`.
- `vw_CurrentStock`: product/category/variant names and current quantities.
- `vw_MonthlySummary`: `TxnYear`, `TxnMonth`, `QtyOut`, `NetChange`, and product identifiers.

Gaps / decisions:

- Inventory value needs acquisition cost; the existing product and stock models have no cost field. `SaleBillLine.UnitPrice` is a selling price and must not be used as inventory cost.
- Existing low-stock behavior uses the shared fixed threshold of 10. The dashboard fixture's `23` low-stock count and “overstock” category do not follow from a configurable policy in the current schema.
- Forecasting should identify which outbound transaction types represent demand, define case-to-piece conversion, and decide how to treat adjustments and returns.
- AI insights currently have no model/provider and are fixture text only.
- Current simulated purchase-request data does not represent a persisted PR.

### `SearchProductsAsync(query, limit, CancellationToken)`

Use `Products.SKU` and `Products.ProductName`, filtered to active products and joined through `ProductVariants` to `StockBalances` or `vw_CurrentStock` for displayed stock. Supplier search is not supported by the existing schema.

Index notes:

- `IX_Products_SKU` already exists.
- Consider a product-name search index (or full-text index if fuzzy search is required).
- `IX_ProductVariants_ProductID` and the stock views support the product-to-variant join.

### `GetWhatIfParametersAsync(sku, CancellationToken)`

The mock returns parameters for `KB-001`. An EF implementation needs current stock, demand history, replenishment lead time, safety stock, unit cost, holding rate, order fee, and allowed quantity increments. None of those policies/costs are persisted in existing product/stock models.

The pure demo formula is documented in `WhatIfSimulator` XML comments. Its assumptions reproduce order quantity 300 → 4% risk, ฿45,000 order cost, ฿10,500 holding cost, and 72 days of coverage (coverage rounded to the nearest day); these are demo assumptions, not facts inferred from the current database.

## Other endpoint data and persistence

| API | Mock implementation | Likely production backing |
|---|---|---|
| `GET /me` | Claims from the signed-in identity | Identity user/role claims; a display-name/department field is not currently modeled |
| `GET /notifications/unread-count` | Dashboard fixture count | New user-scoped notifications table with status/read timestamp |
| `POST /purchase-requests/auto-generate` | In-memory idempotency and job store; reports completion without writing a PR | Purchase-request header/line tables, job state, owner, timestamps, failure details, and idempotency record |
| `GET /purchase-requests/jobs/{jobId}` | Reads the process-local job store | Durable job store/queue; enforce user ownership |
| `POST /ai/chat` | Stub response | AI provider and optional conversation/message persistence |

## Suggested schema additions (proposal only; no migration created)

- **Suppliers**: supplier identifier, name, contact details, status, timestamps.
- **InventoryCosts**: `ProductID` or `VariantID`, unit acquisition cost, currency, effective-from/to dates. Index by product/variant and effective date.
- **ReorderPolicies**: `VariantID`, reorder point, safety stock, lead-time days, target coverage, preferred supplier, minimum/order-multiple quantities. Unique index on variant.
- **PurchaseRequests** and **PurchaseRequestLines**: owner, status, created/completed timestamps, strategy, SKU/product/variant, requested quantity, supplier and unit-cost snapshot. Index status/created timestamp and request-to-line foreign key.
- **PurchaseRequestJobs** or equivalent queue storage: job ID, owner, state, created/updated/completed timestamps, failure detail.
- **IdempotencyRecords**: user ID, idempotency-key hash, request fingerprint, result/job ID, expiry. Unique index on user/key hash.
- **Notifications**: user ID, type, content/reference, created/read timestamps. Index on user/read status/created timestamp.

Existing useful indexes include `IX_Products_SKU`, `IX_Products_CategoryID`, `IX_ProductVariants_ProductID`, `IX_StockTransactions_Variant_Date`, `IX_StockTransactions_Date`, and the monthly/current-stock views. Validate query plans and workload before adding indexes.

## Mock limitations

`MockPurchaseRequestService` is process-local; its job and idempotency records are lost on restart. It marks the demo job completed immediately and does not create purchase documents. The `EfDashboardService` and EF search/What-if methods intentionally throw `NotImplementedException` until the backend mapping and policies are agreed.
