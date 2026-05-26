# Smart Inventory & Warehouse Management — Project Summary for Agent

## Project Overview

Build a **production-ready, enterprise-grade Smart Inventory & Warehouse Management System** using:

- **Backend:** ASP.NET Core Web API (.NET 8) — Code First approach
- **Database:** PostgreSQL with Entity Framework Core (Code First Migrations)
- **Frontend:** Angular (latest)
- **Architecture:** Layered — Controller → Service Layer → Repository Layer → PostgreSQL
- **File Storage:** Local filesystem (Phase 1) via `IFileStorageService` interface (swappable to Azure Blob Storage in Phase 2)
- **Realtime:** ASP.NET Core SignalR (swappable to Azure SignalR in Phase 2)
- **Phase 2 (later):** Azure Service Bus, Azure Blob Storage, Redis caching, Microservices

<!-- ---
Suppose a product location is:

Zone: Electronics
Aisle: A2
Rack: R5
Bin: B12

Meaning:

Go to Electronics section
→ Walk into Aisle A2
→ Find Rack R5
→ Product is in Bin B12 -->

That's the complete role design. A few important things to note for implementation:

**The "partial" cases are the tricky ones.** Full yes/no permissions are easy — just `[Authorize(Roles="Admin,Manager")]`. The partial cases (Manager sees only their warehouse, Staff sees only their warehouse's stock) require service layer scoping — you query the user's assigned warehouse from the JWT claims and inject it into the `QueryParameters` before the repository call. This is why business rule filtering lives in the service, not the controller.

**Soft delete everywhere.** The Admin "delete" permission doesn't mean `DELETE FROM` in SQL. It means setting `is_active = false`. No record is ever physically deleted — this is critical for audit trails and referential integrity. When staff or managers try to delete, the API returns `403 Forbidden`.

**JWT claims carry role + warehouse.** When a user logs in, the token payload includes `userId`, `role`, and `assignedWarehouseId`. Every service method reads these claims to decide what data to scope — no extra DB lookup needed per request.

**The Viewer role is valuable for your project presentation.** It represents the finance/audit user — someone who needs to see everything but touch nothing. Demonstrating this role cleanly shows your mentor you understand real enterprise access control.

Want to update the project summary markdown with the roles and permission matrix added, or shall we move on to the folder structure next?


## Architecture Pattern

```
Angular Frontend
      ↕ HTTP REST + SignalR
ASP.NET Core Web API (Controllers)
      ↕ Dependency Injection
Service Layer (Business Logic, Validation, Notification Orchestration)
      ↕ Repository Pattern
Repository Layer (EF Core IQueryable, LINQ, Pagination/Filter/Sort)
      ↕ EF Core
PostgreSQL Database
```

### Key Architecture Rules
- Controllers are thin — max 5 lines per action, only reads query params and calls service
- Service Layer validates input, applies business rules, maps Entity → DTO
- Repository Layer is the ONLY place that touches EF Core / IQueryable / LINQ
- Never call `.ToList()` before `.Where()`, `.OrderBy()`, `.Skip()`, `.Take()`
- All filtering, sorting, pagination happen at DB level via IQueryable (translated to SQL by EF Core)
- Every external service (SMS, Email, File Storage, Realtime) is behind an interface for easy swapping

---

## Modules

### Core Modules (Required)

#### 1. Auth & Users
- JWT access token + refresh token authentication
- Role-based access control: Admin, Manager, Staff, Viewer
- User–warehouse assignment with access levels (read-only, operator, manager)
- Password reset via email OTP
- Audit trail for every create/update/delete action
- User preferences: notification channel opt-ins (SMS, email, in-app)

#### 2. Inventory Tracking
- Product catalog with SKU, variants, categories (self-referencing subcategories)
- Stock levels per product per warehouse per bin location
- Stock movement log (append-only, never updated — new row per movement)
- Reorder point and reorder quantity configurable per product
- Low-stock alerts triggered when stock crosses reorder threshold
- Inventory valuation (FIFO / weighted average)
- Unit of measure support
- Product image upload via local filesystem

#### 3. Supplier Management
- Supplier profiles with full contact directory
- Lead time, payment terms, credit limit per supplier
- Supplier–product mapping with unit price and min order quantity
- Preferred supplier flag per product
- Auto-calculated performance rating from PO delivery history
- Supplier document uploads (contracts, certificates) via local filesystem

#### 4. Purchase Orders
- PO creation with line items (product, quantity, unit price)
- Multi-level approval workflow: Draft → Submitted → Approved → Received → Closed
- Goods Receipt Note (GRN) creation on delivery
- Over-delivery and under-delivery detection and flagging
- Invoice matching against GRN
- Auto stock increase on GRN confirmation (triggers stock_movements insert)
- PO status tracking throughout lifecycle

#### 5. Barcode Management
- Auto barcode generation when product is created
- Format: CODE128 for standard barcodes, QR Code for rich product data
- Library: **ZXing.Net** (backend, Apache 2.0, free) for both barcode and QR generation
- Angular scanning: **ngx-scanner** (MIT, free) — uses device camera via WebRTC
- USB barcode scanner support (acts as keyboard input — no code needed)
- Barcode value = Product SKU (e.g. `PRD-00123`)
- QR code value = JSON payload `{id, name, sku, warehouseId}`
- Variant barcodes: SKU + variant suffix (e.g. `PRD-001-RED-L`)
- Bin location barcodes: Zone+Aisle+Rack+Bin (e.g. `ZA-A3-R2-B5`)
- Transfer shipment QR: Transfer number (e.g. `TRF-2025-00045`)
- Barcode images saved to local filesystem: `/uploads/barcodes/{productId}.png`
- Batch label generation → returns printable PDF/image sheet (browser print)
- Every scan logged to `barcode_scan_logs` with user, warehouse, action, timestamp

#### 6. Warehouse Transfers
- Inter-warehouse transfer requests with approval before dispatch
- Zone → Aisle → Rack → Bin slot-level tracking
- Pick list generation for warehouse staff
- In-transit status visibility
- Auto stock deduct on dispatch, auto stock add on receive
- Transfer items track: quantity_requested, quantity_dispatched, quantity_received separately

### Extra Modules (Full-fledged additions)

#### 7. Stock Adjustments
- Manual stock corrections with reason codes
- Cycle count / physical stocktake support
- Damage, expiry, write-off tracking
- Variance report (expected vs actual)
- Approval required for adjustments above configured threshold

#### 8. Reports & Analytics
- Inventory valuation report
- Stock movement trend charts
- Dead stock and slow-moving items report
- Supplier performance report
- PO fulfilment rate and lead time accuracy
- CSV and PDF export for all reports

#### 9. Notifications
- **In-app:** Real-time bell icon via ASP.NET Core SignalR
- **Email:** SMTP via MailKit (Gmail/Outlook App Password — free)
- **SMS:** Any SMS REST provider via `ISmsService` interface (HTTP call, provider-agnostic)
- Per-user channel preferences (opt-in/out per channel)
- Per-product, per-warehouse alert threshold configuration
- Delivery tracking in `notification_logs` table

#### 10. File Storage
- `IFileStorageService` interface with `LocalFileStorageService` implementation
- Handles: product images, supplier documents, PO attachments, barcode images
- Swap to `AzureBlobStorageService` in Phase 2 — one line change in DI registration

---

## Notification Event Map

| Event | Priority | SMS | Email | In-app | Recipient |
|---|---|---|---|---|---|
| Stock hits reorder point | Critical | ✓ | | ✓ | Warehouse manager |
| Stock reaches zero | Critical | ✓ | | ✓ | Manager + Admin |
| PO delivery overdue | Critical | ✓ | ✓ | ✓ | Manager + Purchaser |
| Transfer dispatched | High | ✓ | | ✓ | Receiving warehouse |
| PO approved / rejected | High | ✓ | ✓ | ✓ | PO creator |
| Goods received (GRN) | High | | ✓ | ✓ | PO creator + Manager |
| Transfer completed | Normal | | ✓ | ✓ | Both warehouse managers |
| Stock adjustment approved | Normal | | ✓ | ✓ | Requester |
| New user account created | Normal | | ✓ | ✓ | New user |
| Daily stock summary | Normal | | ✓ | | All managers (scheduled) |

---

## Pagination, Filtering & Sorting — Rules

- **All filtering, sorting, pagination must happen at the database level** via EF Core IQueryable
- EF Core translates LINQ to SQL — PostgreSQL does the work (OFFSET/LIMIT/WHERE/ORDER BY)
- Repository layer builds the query using IQueryable — never calls `.ToList()` early
- Service layer validates and sanitizes parameters before passing to repository
- Controller only reads `[FromQuery]` params and passes to service

### QueryParameters Pattern
```csharp
// Base class (shared)
public class QueryParameters {
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
    public string SortBy { get; set; } = "created_at";
    public string SortDir { get; set; } = "desc";
}

// Module-specific (extends base)
public class ProductQueryParameters : QueryParameters {
    public Guid? CategoryId { get; set; }
    public Guid? WarehouseId { get; set; }
    public bool? LowStockOnly { get; set; }
    public bool? IsActive { get; set; }
}

// Same pattern for: PurchaseOrderQueryParameters, SupplierQueryParameters,
// TransferQueryParameters, NotificationQueryParameters
```

### PagedResult Pattern
```csharp
public class PagedResult<T> {
    public IEnumerable<T> Data { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
```

---

## Complete Database Schema (30 Tables — Code First)

### Group 1: Auth & Users & Warehouses

**roles** — `id(PK), name, description, created_at`

**users** — `id(PK), full_name, email, password_hash, phone_number, sms_enabled, email_enabled, role_id(FK→roles), is_active, last_login, created_at`

**refresh_tokens** — `id(PK), user_id(FK→users), token, expires_at, is_revoked, created_at`

**audit_logs** — `id(PK), user_id(FK→users), entity_type, entity_id, action, old_values(jsonb), new_values(jsonb), ip_address, created_at`

**warehouses** — `id(PK), name, code, address, city, country, manager_id(FK→users), is_active, created_at`

**warehouse_zones** — `id(PK), warehouse_id(FK→warehouses), name, code, zone_type, is_active`

**bin_locations** — `id(PK), zone_id(FK→warehouse_zones), aisle, rack, bin, barcode, is_active`

**user_warehouse_access** — `id(PK), user_id(FK→users), warehouse_id(FK→warehouses), access_level, granted_at`

### Group 2: Inventory

**categories** — `id(PK), name, slug, parent_id(FK→categories self-ref), description, is_active`

**products** — `id(PK), name, sku, description, category_id(FK→categories), unit_of_measure, cost_price, selling_price, reorder_point, reorder_quantity, is_active, image_path, created_at`

**product_variants** — `id(PK), product_id(FK→products), variant_name, sku_suffix, attributes(jsonb), is_active`

**stock_levels** — `id(PK), product_id(FK→products), warehouse_id(FK→warehouses), bin_location_id(FK→bin_locations), quantity_on_hand, quantity_reserved, quantity_on_order, last_updated`

**stock_movements** — `id(PK), product_id(FK→products), warehouse_id(FK→warehouses), bin_location_id(FK→bin_locations), movement_type, quantity, reference_type, reference_id, performed_by(FK→users), created_at`

**stock_adjustments** — `id(PK), adjustment_number, product_id(FK→products), warehouse_id(FK→warehouses), bin_location_id(FK→bin_locations), performed_by(FK→users), approved_by(FK→users), reason, quantity_before, quantity_after, quantity_change, created_at`

**alert_configurations** — `id(PK), product_id(FK→products), warehouse_id(FK→warehouses), low_stock_threshold, sms_alert, email_alert, in_app_alert, is_active`

### Group 3: Suppliers

**suppliers** — `id(PK), name, code, contact_person, email, phone, address, lead_time_days, payment_terms, credit_limit, rating, is_active, created_at`

**supplier_products** — `id(PK), supplier_id(FK→suppliers), product_id(FK→products), unit_price, lead_time_days, min_order_quantity, is_preferred`

**supplier_performance_logs** — `id(PK), supplier_id(FK→suppliers), po_id(FK→purchase_orders), promised_days, actual_days, fill_rate, notes, created_at`

### Group 4: Purchase Orders

**purchase_orders** — `id(PK), po_number, supplier_id(FK→suppliers), warehouse_id(FK→warehouses), created_by(FK→users), approved_by(FK→users), status, total_amount, expected_delivery, actual_delivery, notes, created_at`

**purchase_order_items** — `id(PK), po_id(FK→purchase_orders), product_id(FK→products), quantity_ordered, quantity_received, unit_price, total_price`

**goods_receipts** — `id(PK), grn_number, po_id(FK→purchase_orders), received_by(FK→users), warehouse_id(FK→warehouses), received_date, status, notes, created_at`

**goods_receipt_items** — `id(PK), grn_id(FK→goods_receipts), po_item_id(FK→purchase_order_items), bin_location_id(FK→bin_locations), quantity_received, quantity_rejected, rejection_reason`

### Group 5: Barcodes

**barcodes** — `id(PK), product_id(FK→products), barcode_value, barcode_type, is_primary, image_path, created_at`

**barcode_scan_logs** — `id(PK), barcode_id(FK→barcodes), scanned_by(FK→users), warehouse_id(FK→warehouses), action, scanned_at`

### Group 6: Warehouse Transfers

**warehouse_transfers** — `id(PK), transfer_number, from_warehouse_id(FK→warehouses), to_warehouse_id(FK→warehouses), requested_by(FK→users), approved_by(FK→users), status, transfer_date, notes, created_at`

**transfer_items** — `id(PK), transfer_id(FK→warehouse_transfers), product_id(FK→products), from_bin_id(FK→bin_locations), to_bin_id(FK→bin_locations), quantity_requested, quantity_dispatched, quantity_received`

### Group 7: Notifications

**notifications** — `id(PK), user_id(FK→users), channel, type, title, message, entity_type, entity_id, is_read, created_at`

**notification_logs** — `id(PK), user_id(FK→users), channel, event_type, recipient, status, error_message, retry_count, sent_at, created_at`

### Group 8: Files

**file_attachments** — `id(PK), entity_type, entity_id, file_name, file_path, mime_type, file_size_bytes, uploaded_by(FK→users), created_at`
> Polymorphic — entity_type can be "product", "supplier", "purchase_order" etc.

---

## PostgreSQL Indexes (must add in migrations)

| Table | Index Columns | Reason |
|---|---|---|
| products | name, sku, category_id, is_active | Most searched columns |
| stock_levels | product_id, warehouse_id | Core lookup — hit on every request |
| stock_movements | product_id, created_at | History sorted by date |
| purchase_orders | supplier_id, status, created_at | Filtered by status constantly |
| notifications | user_id, is_read | Unread count on every page load |
| barcodes | barcode_value (unique) | Scan lookup must be instant |
| warehouse_transfers | from_warehouse_id, to_warehouse_id, status | Transfer tracking |
| audit_logs | user_id, entity_type, created_at | Audit queries |

---

## Service Interfaces (all swappable)

```csharp
IFileStorageService       → LocalFileStorageService (Phase 1) → AzureBlobStorageService (Phase 2)
ISmsService               → Any REST SMS provider (Phase 1)   → Azure Communication Services (Phase 2)
IEmailService             → SmtpEmailService/MailKit (Phase 1) → SendGrid/Azure Email (Phase 2)
IRealtimeService          → SignalR Hub (Phase 1)              → Azure SignalR Service (Phase 2)
INotificationService      → Orchestrates SMS + Email + Realtime (no change in Phase 2)
IBarcodeService           → ZXing.Net + QRCoder (stays same)
IEventPublisher           → Direct method call (Phase 1)       → Azure Service Bus (Phase 2)
```

---

## Phase 1 Deliverables (Current Goal)

1. **ASP.NET Core Web API project** with layered folder structure
2. **EF Core Code First** — all 30 entity classes with proper relationships, data annotations, and Fluent API configurations
3. **PostgreSQL migrations** — all tables + indexes created via `dotnet ef migrations add`
4. **JWT Authentication** — login, refresh token, logout endpoints
5. **CRUD APIs** for all 10 modules with full search, pagination, sorting
6. **Barcode generation API** — ZXing.Net for CODE128 + QR, saved to local filesystem
7. **Notification system** — SignalR hub + SMTP email + SMS via ISmsService
8. **Low-stock alert trigger** — fires on every stock_movements insert
9. **File upload endpoints** — local filesystem via IFileStorageService
10. **Swagger/OpenAPI** documentation auto-generated
11. **Angular frontend** — all module screens with mat-paginator, mat-sort, mat-table
12. **Role-based route guards** in Angular
13. **Deployment steps document**

## Phase 2 Deliverables (Later)

1. Swap `LocalFileStorageService` → `AzureBlobStorageService`
2. Swap direct method calls → Azure Service Bus event publishing
3. Swap local SignalR → Azure SignalR Service
4. Add Redis caching for stock levels and report queries
5. Split Notification module into a separate microservice
6. Add Hangfire for scheduled jobs (daily summary email)
7. Advanced analytics dashboard

---

## Enterprise Logistics & Intelligent Optimization (Service Layer Business Logic)

To elevate the system from basic CRUD operations to a high-performing Decision Support System (DSS) matching modern enterprise operations, the following advanced mathematical models and logistical algorithms are implemented in the service layer using the existing database schema:

### 1. Dynamic ABC Inventory Classification Engine
Enables managers to prioritize capital and security operations based on the economic value of inventory:
- **Class A**: High-value items comprising the top 70% of cumulative inventory valuation. Subject to rigorous weekly/daily auditing.
- **Class B**: Moderate-value items representing the next 20% of inventory value. Audited bi-weekly.
- **Class C**: Low-value items making up the final 10% of value. Audited monthly or quarterly.
*Calculation: Dynamic query over `stock_levels` × `products.selling_price` mapped to a dynamic classification report.*

### 2. Economic Order Quantity (EOQ) Replenishment Model
Instead of ordering static volumes, the purchase ordering flow automatically calculates optimized purchasing batches:
- **Formula**: $EOQ = \sqrt{\frac{2DS}{H}}$
  - $D$ = Annualized Demand (derived dynamically from historical `stock_movements`).
  - $S$ = Ordering Setup Cost (defined in `supplier_products`).
  - $H$ = Unit Holding Cost (calculated as a configured percentage of unit product cost).
*Outcome: Automatically recommends mathematically optimized order sizes on purchase requisition creation.*

### 3. Inventory Turnover Ratio (ITR) & Dead Stock Analyzer
Identifies slow-moving capital and obsolete inventory to minimize carrying costs:
- **Formula**: $ITR = \frac{\text{Cost of Goods Sold (COGS)}}{\text{Average Inventory Value}}$
- **Dead Stock Flagging**: Flags items with zero outbound movement within the last 90/180 days.
*Outcome: Triggers high-priority notifications to managers advising clearance, promotions, or bulk write-offs.*

### 4. Batched Inventory Valuation Engine (FIFO & Weighted Average)
Ensures strict accounting compliance and accurate balance-sheet reporting of warehouse stock assets:
- **FIFO (First-In, First-Out)**: Automatically walks the `goods_receipt_items` ledger chronologically to evaluate inventory cost by assuming older batches are sold first.
- **Weighted Average**: Tracks a rolling average cost per unit across all active physical batches when received.

### 5. Automated Discrepancy Auditing (Cycle Counting & Shrinkage)
Establishes a rigorous workflow for cross-referencing physical audits with digital registers:
- **Cycle Counting**: Staff perform spot counts in a bin location and submit via a `StockAdjustmentCreateDto`.
- **Shrinkage Variance**: System automatically calculates count variances and flags potential shrinkage (theft, administrative processing errors).
- **Threshold Escalation**: If the variance exceeds 5% of stock volume or $100 in value, the service automatically triggers an `ApprovalRequiredException`, blocks the change, and flags it to the Admin.

---

## What to Build First (Agent Instructions)

> Start with the **ASP.NET Core Web API backend** using **Code First approach**.

### Step 1 — Project & Folder Structure
Create a solution with the following projects:
- `SmartInventory.API` — ASP.NET Core Web API (Controllers, Middleware, Program.cs)
- `SmartInventory.Core` — Entities, Interfaces, DTOs, QueryParameters, PagedResult, Enums
- `SmartInventory.Service` — Service implementations
- `SmartInventory.Repository` — EF Core DbContext, Repository implementations, Migrations
- `SmartInventory.Infrastructure` — Email, SMS, File Storage, Barcode, SignalR implementations

### Step 2 — EF Core Entities
Create all 30 entity classes in `SmartInventory.Core/Entities/` matching the schema above.
- Use `Guid` as primary key for all entities
- Use `IFileStorageService` pattern for file path columns (store path string only)
- Add all navigation properties and foreign keys
- Configure relationships in `DbContext` using Fluent API (OnModelCreating)
- Add JSONB column support for `audit_logs.old_values`, `audit_logs.new_values`, `product_variants.attributes`

### Step 3 — DbContext & Migration
- Create `AppDbContext` with all 30 DbSets
- Add all indexes in `OnModelCreating`
- Run `dotnet ef migrations add InitialCreate`
- Run `dotnet ef database update`
- Seed: default roles (Admin, Manager, Staff, Viewer) and one Admin user

### Step 4 — Repository Layer
- Create `IGenericRepository<T>` with `GetAllAsync(QueryParameters)`, `GetByIdAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`
- Create `PagedResult<T>` class
- Implement per-module repositories: `IProductRepository`, `ISupplierRepository`, `IPurchaseOrderRepository`, `ITransferRepository`, `IBarcodeRepository`, `INotificationRepository`
- All list queries use IQueryable with `.Where()` → `.OrderBy()` → `.CountAsync()` → `.Skip().Take().ToListAsync()`
- Unit of Work pattern wrapping all repositories

### Step 5 — Service Layer
- Implement all service interfaces
- Validate & sanitize QueryParameters (clamp page size 1–100, whitelist sortBy columns)
- Apply business rules (staff sees only their warehouse)
- Map Entity ↔ DTO using AutoMapper

### Step 6 — Auth
- JWT token generation with claims (userId, role, warehouseId)
- Refresh token rotation
- `[Authorize(Roles = "Admin,Manager")]` on relevant endpoints

### Step 7 — API Controllers
- One controller per module
- All list endpoints use `[FromQuery] ModuleQueryParameters params`
- Thin controllers — delegate everything to service layer

### Step 8 — Infrastructure Services
- `LocalFileStorageService` implementing `IFileStorageService`
- `ZXingBarcodeService` implementing `IBarcodeService` (ZXing.Net NuGet)
- `SmtpEmailService` implementing `IEmailService` (MailKit NuGet)
- `SmsService` implementing `ISmsService` (HttpClient REST call)
- `NotificationService` orchestrating all three channels
- SignalR `NotificationHub` for real-time in-app alerts

### Step 9 — Low-stock Alert Trigger
After every `stock_movements` insert, check if `stock_levels.quantity_on_hand <= products.reorder_point`. If yes, call `INotificationService.SendLowStockAlertAsync()` which fires SMS + in-app based on `alert_configurations`.

### Step 10 — Swagger
Add Swagger with JWT bearer auth support so all endpoints are testable from the browser.

---

## NuGet Packages Required

| Package | Purpose |
|---|---|
| `Microsoft.EntityFrameworkCore` | ORM |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | PostgreSQL EF Core provider |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | JWT auth |
| `AutoMapper.Extensions.Microsoft.DependencyInjection` | Entity ↔ DTO mapping |
| `ZXing.Net` | Barcode + QR code generation |
| `QRCoder` | QR code generation (optional, if not using ZXing for QR) |
| `MailKit` | SMTP email sending |
| `Microsoft.AspNetCore.SignalR` | Real-time in-app notifications |
| `Swashbuckle.AspNetCore` | Swagger / OpenAPI |
| `BCrypt.Net-Next` | Password hashing |
| `FluentValidation.AspNetCore` | Request validation |
| `Serilog.AspNetCore` | Structured logging |

## Angular Packages Required

| Package | Purpose |
|---|---|
| `@angular/material` | UI components (table, paginator, sort, form fields) |
| `@microsoft/signalr` | SignalR client for real-time notifications |
| `ngx-scanner` (ZXing) | Camera barcode scanning |
| `ngx-barcode` | Display barcode images in UI |
| `chart.js` or `ng2-charts` | Reports & analytics charts |

---

## Environment Configuration (appsettings.json structure)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=smart_inventory;Username=postgres;Password=yourpassword"
  },
  "JwtSettings": {
    "SecretKey": "your-256-bit-secret",
    "Issuer": "SmartInventoryAPI",
    "Audience": "SmartInventoryClient",
    "AccessTokenExpiryMinutes": 60,
    "RefreshTokenExpiryDays": 7
  },
  "EmailSettings": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SenderEmail": "your@gmail.com",
    "SenderName": "Smart Inventory",
    "AppPassword": "your-gmail-app-password"
  },
  "SmsSettings": {
    "ApiUrl": "https://your-sms-provider/api/send",
    "ApiKey": "your-api-key",
    "SenderId": "SMINV"
  },
  "FileStorage": {
    "BasePath": "uploads",
    "BaseUrl": "https://localhost:5001/uploads"
  }
}
```

---

*Generated from full project design conversation. Code First approach. Phase 1 target. All Azure integrations deferred to Phase 2 via interface abstraction.*
