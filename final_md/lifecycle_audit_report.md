# SmartInventory Lifecycle Audit Report
**Role**: Principal Solution Architect  
**Scope**: End-to-end operational trace, workflow events, background services, and audit logging.

This document traces the complete lifecycle of major operations in the SmartInventory ecosystem. It documents exactly what services are executed, how the database is affected, and how the transactional outbox guarantees reliable downstream processing.

---

## Part 1: Core Architectural Flow (The "Timeline")

To understand the system, we must trace a complete transaction end-to-end. Here is the operational trace of a single action (e.g., creating a user).

### Example Timeline: Admin Creates User

1. **API Call**: Client calls `POST /api/v1/Users` with `UserCreateDto`.
2. **Service Execution**: `UserService.CreateUserAsync` executes.
   - It hashes a generated plain-text token.
   - It instantiates a `User` entity.
   - It instantiates an `OutboxMessage` for the `SendWelcomeEmail` event.
3. **EF Core Interception**: `AppDbContext.SaveChangesAsync()` is called.
   - `OnBeforeSaveChanges()` iterates over tracked entities.
   - It sees the new `User` entity and generates an `AuditLog` record (`Action="Create"`).
4. **Database Commit**: The `User`, `AuditLog`, and `OutboxMessage` are committed atomically in a single PostgreSQL transaction.
5. **Database Trigger**: Because an `OutboxMessage` was inserted, the DB triggers a PostgreSQL `NOTIFY outbox_ready`.
6. **Background Worker Wakeup**: `OutboxProcessorService` (listening via Npgsql) instantly wakes up.
7. **Outbox Processor Execution**: 
   - Queries `outbox_messages` using `SKIP LOCKED` to safely grab pending messages.
   - Routes the message to `IEmailService` (or `INotificationService`).
8. **Downstream Action**: The SMTP email is sent.
9. **Final State**: The `OutboxMessage` status is updated to `Processed`.

---

## Part 2: Workflow Audits

### 1. User Creation
- **API Endpoint**: `POST /api/v1/Users`
- **Service Method**: `UserService.CreateUserAsync`
- **Database Tables Updated**: `users`
- **Audit Logs Created**: `Action="Create", EntityType="User"`
- **Outbox Messages Created**: Yes. Event: `SendWelcomeEmail` (contains `Email` and `PlainToken`).
- **Background Jobs Triggered**: `OutboxProcessorService` picks up the message instantly.
- **Notifications Sent**: Welcome Email with password setup link sent via SMTP.
- **Events Published**: None externally.

### 2. User Login
- **API Endpoint**: `POST /api/v1/Auth/login`
- **Service Method**: `AuthService.SignInAsync`
- **Database Tables Updated**: `users` (updates `LastLogin`), `refresh_tokens` (inserts new hashed token).
- **Audit Logs Created**: `Action="Update", EntityType="User"` (due to `LastLogin` modification), `Action="Create", EntityType="RefreshToken"`.
- **Outbox Messages Created**: No.
- **Background Jobs Triggered**: None.
- **Final Database State**: User logged in, new JWT generated, hashed refresh token saved.

### 3. Product Creation
- **API Endpoint**: `POST /api/v1/Products`
- **Service Method**: `ProductService.CreateProductAsync`
- **Database Tables Updated**: `products`, `product_suppliers` (if linked).
- **Audit Logs Created**: `Action="Create", EntityType="Product"`.
- **Outbox Messages Created**: No.
- **Final Database State**: New product added to the catalog.

### 4. Supplier Registration
- **API Endpoint**: `POST /api/v1/Suppliers/Portal/Register`
- **Service Method**: `SupplierAuthService.RegisterSupplierAsync`
- **Database Tables Updated**: `suppliers`, `supplier_users`.
- **Audit Logs Created**: `Action="Create", EntityType="Supplier"`.
- **Outbox Messages Created**: Yes. `SupplierWelcomeEmail`.
- **Final Database State**: Supplier exists in `PendingVerification` status.

### 5. Purchase Order Creation
- **API Endpoint**: `POST /api/v1/PurchaseOrders`
- **Service Method**: `PurchaseOrderService.CreatePOAsync`
- **Database Tables Updated**: `purchase_orders`, `purchase_order_lines`.
- **Audit Logs Created**: `Action="Create", EntityType="PurchaseOrder"`.
- **Outbox Messages Created**: Yes. `POCreated` (triggering a notification to the supplier/manager).
- **Final Database State**: PO saved as `Draft` or `PendingApproval`.

### 6. Goods Receipt (GRN)
- **API Endpoint**: `POST /api/v1/PurchaseOrders/{id}/Receive`
- **Service Method**: `PurchaseOrderService.ReceiveGoodsAsync`
- **Database Tables Updated**: `goods_receipts`, `goods_receipt_lines`, `purchase_orders` (updates status/received qty), `stock_levels` (increments qty), `stock_movements` (records IN movement).
- **Audit Logs Created**: High volume. Logs for GRN, PO updates, Stock Levels, and Stock Movements.
- **Outbox Messages Created**: Yes. `AppDbContext.HandleStockLevelOutbox()` automatically creates `StockLevelChanged` messages for UI synchronization.
- **Final Database State**: Goods recorded, stock incremented, PO partially or fully fulfilled.

### 7. Warehouse Transfer Dispatch
- **API Endpoint**: `POST /api/v1/Transfers/{id}/Dispatch`
- **Service Method**: `TransferService.DispatchTransferAsync`
- **Database Tables Updated**: `transfers` (Status = InTransit), `transfer_items`, `stock_levels` (decrements source stock), `stock_movements` (records OUT movement).
- **Audit Logs Created**: Yes, on all updated entities.
- **Outbox Messages Created**: Yes. `TransferDispatched` and auto-generated `StockLevelChanged` messages.
- **Final Database State**: Inventory leaves the source warehouse and is marked as InTransit.

### 8. File Upload
- **API Endpoint**: Internal consumption (e.g. Supplier Documents).
- **Service Method**: `FileStorageService.UploadFileAsync`
- **Database Tables Updated**: None directly (Saves file to local `/uploads` directory).
- **Final Database State**: Returns a relative URI stored in parent entities (e.g., `DocumentUrl`).

---

## Part 3: Background Processing & System Services

### 1. OutboxProcessorService
- **When does it run?** It runs continuously as a HostedService.
- **What wakes it up?** It wakes instantly via a `NOTIFY outbox_ready` PostgreSQL trigger, OR via a 5-minute fallback polling loop.
- **What records does it read?** Reads `OutboxMessage` rows with Status = `Pending` or `Failed`.
- **How does SKIP LOCKED work?** It executes `SELECT * FROM outbox_messages WHERE Status = 'Pending' FOR UPDATE SKIP LOCKED LIMIT 10`. This ensures that if multiple instances of the API are running, they will not grab the same messages, allowing infinite horizontal scaling.
- **What happens if processing fails?** `RetryCount` increments. If `RetryCount >= 3`, status becomes `DeadLetter`. Otherwise, it remains `Failed` and is retried.

### 2. POOverdueCheckerJob
- **Schedule**: Runs every 24 hours.
- **Query logic**: Scans `PurchaseOrder` table where `Status == Approved`, `ExpectedDelivery < DateTime.UtcNow`, and `ActualDelivery == null`.
- **Actions performed**: For every overdue PO, it loops and calls `INotificationService.SendPOOverdueAlertAsync(poId)`.
- **Notifications generated**: Inserts rows into `notification_logs` to alert managers.

### 3. AuditLogArchiveJob
- **Schedule**: Runs daily at 02:00 UTC.
- **Archive criteria**: Queries `AuditLogs` where `CreatedAt` is older than 365 days.
- **Tables involved**: Reads from `audit_logs`, copies to `audit_log_archives`, then deletes from `audit_logs`.
- **Transaction Safety**: Processes in batches of 1,000 to prevent locking up the database during heavy enterprise load.

### 4. NotificationService
- **When notifications are created**: Primarily triggered by the OutboxProcessor, or directly invoked for internal DB logging (e.g. PO Overdue alerts).
- **Storage**: Real notifications (App UI) are stored in `notification_logs`.

### 5. Audit Logging Architecture
- **When are they created?** In `AppDbContext.OnBeforeSaveChanges()`.
- **Which entities are audited?** ALL entities inheriting from `BaseEntity` are audited during Create, Update, and Delete operations.
- **What data is captured?** `Action` type, `EntityType`, `IpAddress`, `UserId`, and JSON snapshots of `OldValues` and `NewValues`.
- **How is CurrentUser used?** `ICurrentUserService` is injected into `AppDbContext` to extract the `UserId` from the current HTTP Request context. If no user is logged in (e.g., background job), it defaults to `Guid.Empty` (System).
