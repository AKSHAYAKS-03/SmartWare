# SmartInventory WMS - Complete Operational Handbook

## TABLE OF CONTENTS

1. [SYSTEM INVENTORY](#phase-1---system-inventory)
2. [DATA SEED](#phase-2---complete-data-seed)
3. [COMPLETE BUSINESS FLOW](#phase-3---complete-business-flow)
4. [TRANSFER VARIANCE SCENARIO](#phase-4---transfer-variance-scenario)
5. [BARCODE FLOW](#phase-5---barcode-flow)
6. [OUTBOX FLOW](#phase-6---outbox-flow)
7. [AUDIT FLOW](#phase-7---audit-flow)
8. [TEST PLAN](#phase-8---test-plan)

---

## PHASE 1 — SYSTEM INVENTORY

### 1.1 CONTROLLERS AND ENDPOINTS

#### Internal API Controllers

| Controller | Route Prefix | Authorization |
|------------|-------------|---------------|
| **AuthController** | `/api/v1/Auth` | Public |
| **CategoriesController** | `/api/v1/Categories` | Require Admin/Manager |
| **ProductsController** | `/api/v1/Products` | Require Manager/Admin |
| **WarehousesController** | `/api/v1/Warehouses` | Require Admin/Manager |
| **PurchaseOrdersController** | `/api/v1/PurchaseOrders` | Require Manager/Staff |
| **StockAdjustmentsController** | `/api/v1/StockAdjustments` | Require Staff/Manager |
| **TransfersController** | `/api/v1/Transfers` | Require Staff/Manager |
| **BarcodesController** | `/api/v1/Barcodes` | Require Manager/Staff |
| **NotificationsController** | `/api/v1/Notifications` | Require Authentication |
| **SuppliersController** | `/api/v1/Suppliers` | Require Manager/Admin |
| **FilesController** | `/api/v1/Files` | Require Authentication |
| **InvoicesController** | `/api/v1/Invoices` | Require Manager |
| **ReportsController** | `/api/v1/Reports` | Require Viewer/Admin |
| **UsersController** | `/api/v1/Users` | Require Admin |
| **LookupController** | `/api/v1/Lookup` | Require Authentication |

#### Supplier Portal Controllers

| Controller | Route Prefix | Authorization |
|------------|-------------|---------------|
| **SupplierAuthController** | `/api/v1/supplier/auth` | Public |
| **SupplierDashboardController** | `/api/v1/supplier/dashboard` | Require Supplier |
| **SupplierPurchaseOrdersController** | `/api/v1/supplier/purchase-orders` | Require Supplier |
| **SupplierInvoicesController** | `/api/v1/supplier/invoices` | Require Supplier |
| **SupplierCatalogueController** | `/api/v1/supplier/catalogue` | Require Supplier |
| **SupplierProfileController** | `/api/v1/supplier/profile` | Require Supplier |

---

### 1.2 ALL ENDPOINTS (Complete List)

#### AuthController - `/api/v1/Auth`

| Method | Endpoint | Purpose | Request Body | Response |
|--------|----------|---------|--------------|----------|
| POST | `/signin` | Login internal user | LoginDto | JWT tokens |
| POST | `/refresh` | Refresh token | RefreshTokenDto | New tokens |
| POST | `/revoke` | Logout | RefreshTokenDto | 204 |
| PUT | `/change-password` | Change password | ChangePasswordDto | 204 |
| POST | `/set-password` | Set initial password | SetPasswordDto | 200 |

#### WarehousesController - `/api/v1/Warehouses`

| Method | Endpoint | Purpose | Request Body | Response |
|--------|----------|---------|--------------|----------|
| GET | `/` | List warehouses | QueryParameters | PagedResult<Warehouse> |
| GET | `/{id}` | Get warehouse details | - | WarehouseResponseDto |
| POST | `/` | Create warehouse | WarehouseCreateDto | WarehouseResponseDto |
| PUT | `/{id}` | Update warehouse | WarehouseUpdateDto | WarehouseResponseDto |
| DELETE | `/{id}` | Soft delete warehouse | - | 204 |
| GET | `/{id}/zones` | List zones | - | List<Zone> |
| POST | `/{id}/zones` | Create zone | ZoneCreateDto | ZoneResponseDto |
| PUT | `/zones/{zoneId}` | Update zone | ZoneUpdateDto | ZoneResponseDto |
| DELETE | `/zones/{zoneId}` | Delete zone | - | 204 |
| GET | `/zones/{zoneId}/bins` | List bins | - | List<BinLocation> |
| POST | `/zones/{zoneId}/bins` | Create bin | BinLocationCreateDto | BinLocationResponseDto |
| PUT | `/bins/{binId}` | Update bin | BinLocationUpdateDto | BinLocationResponseDto |
| DELETE | `/bins/{binId}` | Delete bin | - | 204 |
| GET | `/{id}/users` | List warehouse users | - | List<UserWarehouseAccess> |
| POST | `/{id}/users` | Assign user access | UserWarehouseAccessCreateDto | UserWarehouseAccess |
| DELETE | `/access/{accessId}` | Revoke user access | - | 204 |
| GET | `/{id}/putaway-suggestion` | Get putaway suggestion | productId query | BinLocationDto |

#### CategoriesController - `/api/v1/Categories`

| Method | Endpoint | Purpose | Request Body | Response |
|--------|----------|---------|--------------|----------|
| GET | `/` | List categories | QueryParameters | PagedResult<Category> |
| GET | `/tree` | Get category tree | - | List<CategoryTreeDto> |
| GET | `/{id}` | Get category | - | CategoryResponseDto |
| POST | `/` | Create category | CategoryCreateDto | CategoryResponseDto |
| PUT | `/{id}` | Update category | CategoryUpdateDto | CategoryResponseDto |
| DELETE | `/{id}` | Delete category | - | 204 |

#### ProductsController - `/api/v1/Products`

| Method | Endpoint | Purpose | Request Body | Response |
|--------|----------|---------|--------------|----------|
| GET | `/` | List products | ProductQueryParameters | PagedResult<Product> |
| POST | `/search` | Search products | DynamicQueryRequest | PagedResult<Product> |
| GET | `/{id}` | Get product | - | ProductResponseDto |
| POST | `/` | Create product | ProductCreateDto | ProductResponseDto |
| PUT | `/{id}` | Update product | ProductUpdateDto | ProductResponseDto |
| DELETE | `/{id}` | Delete product | - | 204 |
| GET | `/low-stock` | Get low stock products | warehouseId query | List<ProductLowStockDto> |
| GET | `/{id}/eoq` | Get EOQ | - | EOQ calculation |
| GET | `/abc-classification` | Get ABC classification | warehouseId query | List<AbcClassificationDto> |
| POST | `/{warehouseId}/update-abc` | Update ABC categories | - | 204 |

#### PurchaseOrdersController - `/api/v1/PurchaseOrders`

| Method | Endpoint | Purpose | Request Body | Response |
|--------|----------|---------|--------------|----------|
| GET | `/` | List POs | PurchaseOrderQueryParameters | PagedResult<PO> |
| POST | `/search` | Search POs | DynamicQueryRequest | PagedResult<PO> |
| GET | `/{id}` | Get PO | - | PurchaseOrderResponseDto |
| POST | `/` | Create PO | PurchaseOrderCreateDto | PurchaseOrderResponseDto |
| PUT | `/{id}` | Update PO | PurchaseOrderUpdateDto | PurchaseOrderResponseDto |
| PUT | `/{id}/submit` | Submit for approval | - | PurchaseOrderResponseDto |
| PUT | `/{id}/approve` | Approve/Reject PO | PurchaseOrderApprovalDto | PurchaseOrderResponseDto |
| PUT | `/{id}/cancel` | Cancel PO | - | PurchaseOrderResponseDto |
| POST | `/{id}/grn` | Receive goods | GoodsReceiptCreateDto | GoodsReceiptResponseDto |
| POST | `/{id}/grn/bulk` | Bulk GRN import | CSV file | GoodsReceiptResponseDto |
| GET | `/{id}/grn` | List GRNs | - | List<GoodsReceipt> |
| POST | `/receipts/{receiptId}/cancel` | Cancel GRN | - | 200 |

#### StockAdjustmentsController - `/api/v1/StockAdjustments`

| Method | Endpoint | Purpose | Request Body | Response |
|--------|----------|---------|--------------|----------|
| GET | `/` | List adjustments | StockAdjustmentQueryParameters | PagedResult<Adjustment> |
| GET | `/{id}` | Get adjustment | - | StockAdjustmentResponseDto |
| POST | `/` | Create adjustment | StockAdjustmentCreateDto | StockAdjustmentResponseDto (or 202) |
| PUT | `/{id}/approve` | Approve adjustment | StockAdjustmentApprovalDto | StockAdjustmentResponseDto |
| POST | `/{id}/cancel` | Cancel adjustment | - | 200 |

#### TransfersController - `/api/v1/Transfers`

| Method | Endpoint | Purpose | Request Body | Response |
|--------|----------|---------|--------------|----------|
| GET | `/` | List transfers | TransferQueryParameters | PagedResult<Transfer> |
| POST | `/search` | Search transfers | DynamicQueryRequest | PagedResult<Transfer> |
| GET | `/{id}` | Get transfer | - | TransferResponseDto |
| POST | `/` | Create transfer | TransferCreateDto | TransferResponseDto |
| PUT | `/{id}/approve` | Approve transfer | TransferApprovalDto | TransferResponseDto |
| PUT | `/{id}/dispatch` | Dispatch transfer | - | TransferResponseDto |
| PUT | `/{id}/receive` | Receive transfer | TransferReceiveDto | TransferResponseDto |
| POST | `/bin-to-bin` | Bin-to-bin transfer | BinTransferCreateDto | 200 |

#### BarcodesController - `/api/v1/Barcodes`

| Method | Endpoint | Purpose | Request Body | Response |
|--------|----------|---------|--------------|----------|
| POST | `/generate` | Generate barcode | BarcodeGenerateRequestDto | BarcodeResponseDto |
| POST | `/batch-generate` | Batch generate barcodes | List<BarcodeGenerateRequestDto> | List<BarcodeResponseDto> |
| POST | `/scan` | Scan barcode | BarcodeScanDto | ScanResultDto |
| GET | `/product/{productId}` | Get product barcodes | - | List<BarcodeResponseDto> |
| GET | `/{id}/image` | Get barcode image | - | File (BMP) |

#### NotificationsController - `/api/v1/Notifications`

| Method | Endpoint | Purpose | Request Body | Response |
|--------|----------|---------|--------------|----------|
| GET | `/` | Get user inbox | QueryParameters | PagedResult<Notification> |
| GET | `/unread-count` | Get unread count | - | { count: int } |
| PUT | `/{id}/read` | Mark notification read | - | 204 |
| PUT | `/read-all` | Mark all read | - | 204 |

#### SuppliersController - `/api/v1/Suppliers`

| Method | Endpoint | Purpose | Request Body | Response |
|--------|----------|---------|--------------|----------|
| GET | `/` | List suppliers | SupplierQueryParameters | PagedResult<Supplier> |
| GET | `/{id}` | Get supplier | - | SupplierResponseDto |
| POST | `/` | Create supplier | SupplierCreateDto | SupplierResponseDto |
| PUT | `/{id}` | Update supplier | SupplierUpdateDto | SupplierResponseDto |
| DELETE | `/{id}` | Delete supplier | - | 204 |
| GET | `/{id}/products` | Get supplier products | - | List<SupplierProduct> |
| POST | `/{id}/products` | Add supplier product | SupplierProductCreateDto | SupplierProduct |
| PUT | `/products/{supplierProductId}` | Update supplier product | SupplierProductUpdateDto | SupplierProduct |
| DELETE | `/products/{supplierProductId}` | Remove supplier product | - | 204 |
| GET | `/{id}/performance` | Get supplier performance | - | SupplierPerformanceDto |
| POST | `/{id}/recalculate-rating` | Recalculate rating | - | 204 |
| POST | `/invite` | Invite supplier | SupplierInviteRequest | 200 |
| GET | `/pending-reviews` | Get pending reviews | - | List<SupplierPendingReviewDto> |
| POST | `/{id}/review` | Review supplier | SupplierReviewRequest | 200 |
| POST | `/{id}/suspend` | Suspend supplier | SupplierSuspendRequest | 204 |
| POST | `/{id}/activate` | Activate supplier | - | 204 |

#### SupplierAuthController - `/api/v1/supplier/auth`

| Method | Endpoint | Purpose | Request Body | Response |
|--------|----------|---------|--------------|----------|
| POST | `/login` | Supplier login | SupplierLoginRequest | JWT tokens |
| POST | `/refresh` | Refresh token | SupplierRefreshTokenRequest | New tokens |
| POST | `/logout` | Logout | SupplierRefreshTokenRequest | 204 |
| PUT | `/change-password` | Change password | SupplierChangePasswordRequest | 204 |
| POST | `/register` | Self-register | SupplierRegisterRequest | 200 |
| POST | `/verify-email` | Verify email | SupplierVerifyEmailRequest | 200 |
| POST | `/complete-registration` | Complete registration | SupplierCompleteRegistrationRequest | 200 |

#### SupplierDashboardController - `/api/v1/supplier/dashboard`

| Method | Endpoint | Purpose | Request Body | Response |
|--------|----------|---------|--------------|----------|
| GET | `/` | Get dashboard | - | SupplierDashboardDto |

#### SupplierPurchaseOrdersController - `/api/v1/supplier/purchase-orders`

| Method | Endpoint | Purpose | Request Body | Response |
|--------|----------|---------|--------------|----------|
| GET | `/` | Get my POs | - | List<SupplierPODto> |
| GET | `/{id}` | Get PO detail | - | SupplierPODetailDto |
| POST | `/{id}/respond` | Accept/decline PO | SupplierRespondToPORequest | 200 |
| PUT | `/{id}/delivery-date` | Update delivery date | SupplierUpdateDeliveryDateRequest | 200 |
| POST | `/{id}/dispatch` | Mark dispatched | SupplierMarkDispatchedRequest | 200 |

#### SupplierInvoicesController - `/api/v1/supplier/invoices`

| Method | Endpoint | Purpose | Request Body | Response |
|--------|----------|---------|--------------|----------|
| POST | `/` | Upload invoice | Multipart form | SupplierInvoiceDto |
| GET | `/` | Get my invoices | - | List<SupplierInvoiceDto> |
| GET | `/{id}` | Get invoice detail | - | SupplierInvoiceDetailDto |
| GET | `/{id}/download` | Download PDF | - | File |

#### SupplierCatalogueController - `/api/v1/supplier/catalogue`

| Method | Endpoint | Purpose | Request Body | Response |
|--------|----------|---------|--------------|----------|
| GET | `/` | Get my catalogue | - | List<CatalogueItemDto> |
| POST | `/` | Add catalogue item | SupplierAddCatalogueItemRequest | CatalogueItemDto |
| PUT | `/{id}` | Update catalogue item | SupplierUpdateCatalogueItemRequest | 200 |
| DELETE | `/{id}` | Deactivate item | - | 200 |

#### SupplierProfileController - `/api/v1/supplier/profile`

| Method | Endpoint | Purpose | Request Body | Response |
|--------|----------|---------|--------------|----------|
| GET | `/` | Get profile | - | SupplierProfileDto |
| PUT | `/` | Update profile | SupplierUpdateProfileRequest | 200 |
| POST | `/logo` | Upload logo | Multipart form | 200 |
| GET | `/status` | Get onboarding status | - | OnboardingStatusDto |
| POST | `/submit-info` | Submit info | SupplierSubmitInfoRequest | 200 |
| GET | `/agreement` | Get agreement | - | { agreementText } |
| POST | `/agreement/accept` | Accept agreement | - | 200 |

#### InvoicesController - `/api/v1/Invoices`

| Method | Endpoint | Purpose | Request Body | Response |
|--------|----------|---------|--------------|----------|
| POST | `/{id}/under-review` | Mark under review | InvoiceActionDto | 204 |
| POST | `/{id}/match` | Match invoice | InvoiceActionDto | InvoiceActionResult |
| POST | `/{id}/pay` | Pay invoice | InvoicePayDto | 204 |
| POST | `/{id}/void` | Void invoice | InvoiceRejectDto | 204 |

#### ReportsController - `/api/v1/Reports`

| Method | Endpoint | Purpose | Query Parameters | Response |
|--------|----------|---------|------------------|----------|
| GET | `/inventory-valuation` | Inventory valuation | warehouseId, method, export | CSV or ValuationReport |
| GET | `/stock-movements` | Stock movements | warehouseId, productId, from, to | CSV or MovementReport |
| GET | `/dead-stock` | Dead stock report | warehouseId, daysThreshold | CSV or DeadStockReport |
| GET | `/shrinkage` | Shrinkage report | warehouseId, from, to | CSV or ShrinkageReport |
| GET | `/supplier-performance` | Supplier performance | supplierId, warehouseId | CSV or PerformanceReport |
| GET | `/po-fulfillment` | PO fulfillment | warehouseId, from, to | CSV or FulfillmentReport |
| GET | `/audit-log` | Audit log | QueryParameters | PagedResult<AuditLog> |
| GET | `/warehouse-utilization` | Warehouse utilization | warehouseId | CSV or UtilizationReport |
| GET | `/override-audit` | Override audit | warehouseId, from, to | CSV or OverrideAuditReport |

---

### 1.3 MAJOR ENTITIES

#### User & Authentication
- **User**: Internal system users with roles, permissions, authentication
- **Role**: Role definitions with permissions (Admin, Manager, Staff, Viewer)
- **RefreshToken**: JWT refresh tokens
- **SupplierRefreshToken**: Supplier-specific refresh tokens

#### Warehouse Structure
- **Warehouse**: Physical warehouse with capacity, status, approval workflow
- **WarehouseZone**: Zones (Storage, Receiving, Shipping, Quality)
- **BinLocation**: Specific storage locations (BinCode)

#### Inventory
- **Product**: Product catalog with SKU, categories, variants
- **ProductVariant**: Product variations
- **StockLevel**: Current stock per product per warehouse per bin
- **StockMovement**: Append-only movement log
- **StockAdjustment**: Manual stock corrections with approval

#### Purchase Orders
- **PurchaseOrder**: PO with multi-level approval workflow
- **PurchaseOrderItem**: PO line items
- **GoodsReceipt**: Goods Receipt Note (GRN)
- **GoodsReceiptItem**: GRN line items

#### Transfers
- **WarehouseTransfer**: Inter-warehouse transfers
- **TransferItem**: Transfer line items

#### Barcodes
- **Barcode**: Barcode/QR code records
- **BarcodeScanLog**: Scan history

#### Notifications
- **Notification**: In-app notifications
- **NotificationLog**: Notification delivery log

#### Suppliers
- **Supplier**: Supplier profile
- **SupplierProduct**: Supplier's product catalogue
- **SupplierContact**: Contact person details
- **SupplierInvoice**: Supplier invoices
- **SupplierPerformanceLog**: Performance tracking

#### Audit & Compliance
- **AuditLog**: All entity changes (CRUD)
- **AuditLogArchive**: Archived audit logs
- **OverrideAuditLog**: Capacity override logs
- **OutboxMessage**: Transactional outbox for notifications

#### File Attachments
- **FileAttachment**: Uploaded files with metadata
- **FileValidationService**: File validation logic

---

### 1.4 DTOs BY CATEGORY

#### Auth DTOs
- **LoginDto**: Email, Password
- **RefreshTokenDto**: RefreshToken
- **ChangePasswordDto**: OldPassword, NewPassword
- **SetPasswordDto**: InviteToken, Password
- **SupplierLoginRequest**: Email, Password
- **SupplierRegisterRequest**: Supplier details (including GSTIN/PAN), contact info
- **SupplierVerifyEmailRequest**: Email, OTP
- **SupplierChangePasswordRequest**: CurrentPassword, NewPassword

#### Warehouse DTOs
- **WarehouseCreateDto**: Name, Code, Address, Tax ID, etc.
- **WarehouseUpdateDto**: All fields except ID
- **ZoneCreateDto**: Name, Code, ZoneType, Capacity settings
- **ZoneUpdateDto**: Update zone details
- **BinLocationCreateDto**: BinCode, Barcode, Capacity
- **BinLocationUpdateDto**: Update bin details
- **UserWarehouseAccessCreateDto**: UserId, AccessLevel, WarehouseId

#### Category DTOs
- **CategoryCreateDto**: Name, Code, ParentId, Description
- **CategoryUpdateDto**: Update category details

#### Product DTOs
- **ProductCreateDto**: Name, SKU, CategoryId, pricing, dimensions
- **ProductUpdateDto**: Update product details
- **ProductQueryParameters**: Filter, search, sort, page
- **DynamicQueryRequest**: Advanced dynamic queries

#### Purchase Order DTOs
- **PurchaseOrderCreateDto**: SupplierId, WarehouseId, Items, ExpectedDelivery
- **PurchaseOrderUpdateDto**: Update PO
- **PurchaseOrderApprovalDto**: Approve/Reject with notes
- **GoodsReceiptCreateDto**: PO Items with received/rejected quantities
- **GoodsReceiptItemDto**: Individual GRN item
- **PurchaseOrderQueryParameters**: Filter by supplier, warehouse, status, date

#### Transfer DTOs
- **TransferCreateDto**: From/To warehouse, Items, requestedBy
- **TransferApprovalDto**: Approve/Reject, approvedBy
- **TransferReceiveDto**: Received quantities per item
- **BinTransferCreateDto**: Bin-to-bin transfer
- **TransferQueryParameters**: Filter by warehouse, status

#### Stock Adjustment DTOs
- **StockAdjustmentCreateDto**: Product, Warehouse, Bin, reason, quantities
- **StockAdjustmentApprovalDto**: Approve/Reject
- **StockAdjustmentQueryParameters**: Filter by product, warehouse, status

#### Barcode DTOs
- **BarcodeGenerateRequestDto**: ProductId, BarcodeValue, Type, IsPrimary
- **BarcodeScanDto**: BarcodeValue, Action
- **ScanResultDto**: Product info, locations, totals
- **BarcodeResponseDto**: Barcode details

#### Supplier DTOs
- **SupplierCreateDto**: Name, Code, GSTIN, PAN, Contact, PaymentTerms
- **SupplierUpdateDto**: Update supplier details including GSTIN, PAN
- **SupplierProductCreateDto**: ProductId, UnitPrice, MinOrderQuantity
- **SupplierProductUpdateDto**: Update catalogue item
- **SupplierInviteRequest**: Email, ContactPerson
- **SupplierReviewRequest**: Approve/Reject with reason
- **SupplierSuspendRequest**: Reason for suspension

#### Supplier Portal DTOs
- **SupplierUploadInvoiceRequest**: Invoice details with file
- **SupplierAddCatalogueItemRequest**: Add product to catalogue
- **SupplierUpdateCatalogueItemRequest**: Update price, lead time
- **SupplierRespondToPORequest**: Accept/decline PO
- **SupplierUpdateDeliveryDateRequest**: New expected date
- **SupplierMarkDispatchedRequest**: Tracking number
- **SupplierUpdateProfileRequest**: Contact person details

#### Notification DTOs
- **NotificationResponseDto**: Notification details for UI

---

### 1.5 BACKGROUND JOBS

| Job | Schedule | Purpose |
|-----|----------|---------|
| **AuditLogArchiveJob** | Daily at 02:00 UTC | Move audit logs >365 days to archive |
| **POOverdueCheckerJob** | Every 24 hours | Check overdue POs and alert managers |

---

### 1.6 CACHING COMPONENTS

| Component | Type | Purpose |
|-----------|------|---------|
| **MemoryCacheService** | IMemoryCache | Fallback caching when Redis unavailable |
| **RedisCacheService** | IConnectionMultiplexer | Distributed caching with Redis |
| **CacheService Interface** | Abstraction | Generic cache operations |

Caching Keys Used:
- `Idempotency_PO_{key}` - PO creation idempotency (24h TTL)
- `Idempotency_GRN_{key}` - GRN creation idempotency (24h TTL)
- `Idempotency_Adj_{key}` - Adjustment idempotency (24h TTL)
- `Idempotency_Transfer_{key}` - Transfer creation idempotency (24h TTL)

---

### 1.7 OUTBOX COMPONENTS

| Component | Type | Purpose |
|-----------|------|---------|
| **OutboxProcessorService** | BackgroundService | Process outbox messages via polling and LISTEN/NOTIFY |
| **OutboxMessage** | Entity | Transactional outbox records |
| **OutboxNotificationPayload** | DTO | Notification delivery data |

Event Types in Outbox:
- `StockLevelChanged` - Real-time stock updates to Redis
- `SendNotification` - Email/SMS/In-app notifications

---

### 1.8 AUDIT COMPONENTS

| Component | Type | Purpose |
|-----------|------|---------|
| **AuditLog** | Entity | Primary audit trail |
| **AuditLogArchive** | Entity | Archived audit logs (7+ years) |
| **OverrideAuditLog** | Entity | Capacity override tracking |
| **AppDbContext.SaveChanges** | Override | Automated audit capture |

Audit Trigger Points:
- All Create, Update, Delete operations on any entity
- Captures: UserId, TableName, EntityId, Action, OldValues, NewValues, IpAddress, CreatedAt

---

### 1.9 SUPPLIER PORTAL FEATURES

| Feature | Purpose | Access |
|---------|---------|--------|
| Self-registration | Suppliers register independently | Public |
| Email verification | Verify supplier contact email | Public |
| Complete registration | Admin-invited suppliers complete profile | Public |
| Login | Supplier authentication | Public |
| Dashboard | View own performance metrics | Supplier role |
| Purchase Orders | View and respond to POs | Supplier role |
| Invoice Upload | Upload invoices against POs | Supplier role |
| Catalogue Management | Manage own product catalogue | Supplier role |
| Profile Management | Update contact details, logo | Supplier role |
| Onboarding Status | View onboarding status | Supplier role |

---

### 1.10 WAREHOUSE FEATURES

| Feature | Purpose |
|---------|---------|
| Multi-level structure | Warehouse → Zone → BinLocation |
| Zone types | Storage, Receiving, Shipping, Quality, Overflow |
| Capacity enforcement | Volume and weight limits per bin |
| Bin type assignment | Standard, Bulk, Hazardous, ColdStorage |
| Putaway suggestions | AI-based bin recommendations |
| User access control | Per-warehouse permissions |
| Manager assignment | Warehouse-specific managers |
| Status workflow | PendingVerification → Active |

---

### 1.11 BARCODE FEATURES

| Feature | Purpose |
|---------|---------|
| Code128/QR generation | Primary and secondary barcodes |
| Scan logging | Track all barcode scans with action |
| Location mapping | Show stock locations on scan |
| Image generation | BMP format barcode images |
| Batch generation | High-volume label printing |

---

### 1.12 INVENTORY FEATURES

| Feature | Purpose |
|---------|---------|
| Stock levels | Track QOH, Reserved, OnOrder, InTransit |
| Stock movements | Append-only movement history |
| ABC classification | Value-based product categorization |
| EOQ calculation | Economic Order Quantity |
| Safety stock alerts | Critical low stock warnings |
| Low stock alerts | Threshold-based notifications |
| Shrinkage tracking | Theft, damage, expiry analysis |
| Inventory valuation | Weighted average costing |

---

### 1.13 VALIDATIONS

| Entity | Validation Rules |
|--------|-----------------|
| **User** | Email uniqueness, password requirements, invite token expiry, strict Indian phone regex (+91) |
| **Warehouse** | Code uniqueness, tax ID encryption, approval workflow |
| **Category** | Name uniqueness, parent-child relationship |
| **Product** | SKU uniqueness, category exists, supplier products active |
| **PurchaseOrder** | Supplier exists, warehouse exists, supplier offers product |
| **PurchaseOrderItem** | Quantity > 0, price matches catalogue, MOQ compliance |
| **StockAdjustment** | Quantity after >= 0, variance threshold triggers approval |
| **Transfer** | Origin has sufficient stock, same warehouse blocked |
| **Barcode** | Value uniqueness, only one primary per product |
| **GRN** | Delivery Challan required, PO approved status |
| **Supplier** | Strict Indian phone regex (+91), 15-char GSTIN, 10-char PAN |

---

---

## PHASE 2 — COMPLETE DATA SEED

### 2.1 ROLES

| Id | Name | Description | Permissions |
|----|------|-------------|-------------|
| `a0d33b91-4567-4eef-b123-999999999901` | Admin | Full system access with administrative rights | Admin, Manage, Inventory, View |
| `a0d33b91-4567-4eef-b123-999999999902` | Manager | Warehouse and inventory management level access | Manage, Inventory, View |
| `a0d33b91-4567-4eef-b123-999999999903` | Staff | Day-to-day warehouse operations access | Inventory, View |
| `a0d33b91-4567-4eef-b123-999999999904` | Viewer | Read-only access to catalogs and reports | View |

### 2.2 DEFAULT ADMIN USER

| Field | Value |
|-------|-------|
| **Id** | `b0d33b91-4567-4eef-b123-888888888801` |
| **FullName** | System Administrator |
| **Email** | admin@smartinventory.com |
| **Password** | Admin@123 (pre-hashed with BCrypt) |
| **PhoneNumber** | +15550199 |
| **SmsEnabled** | false |
| **EmailEnabled** | true |
| **IsActive** | true |
| **Status** | Active |
| **RoleId** | `a0d33b91-4567-4eef-b123-999999999901` (Admin) |
| **CreatedAt** | 2026-01-01 00:00:00 |

### 2.3 SEQUENCE COUNTERS

| Id | EntityName | Prefix | CurrentValue | CreatedAt |
|----|------------|--------|--------------|-----------|
| `c0d33b91-4567-4eef-b123-777777777701` | PurchaseOrder | PO | 0 | 2026-01-01 |
| `c0d33b91-4567-4eef-b123-777777777702` | GoodsReceipt | GRN | - | 2026-01-01 |
| `c0d33b91-4567-4eef-b123-777777777703` | WarehouseTransfer | TRF | - | 2026-01-01 |
| `c0d33b91-4567-4eef-b123-777777777704` | StockAdjustment | ADJ | - | 2026-01-01 |
| `c0d33b91-4567-4eef-b123-777777777705` | Product | PRD | - | 2026-01-01 |
| `c0d33b91-4567-4eef-b123-777777777706` | Supplier | SUP | - | 2026-01-01 |
| `c0d33b91-4567-4eef-b123-777777777707` | Warehouse | WH | - | 2026-01-01 |

---

### 2.4 REALISTIC ENTERPRISE DATASET

#### WAREHOUSES

| Id | Name | Code | Address | City | Country | Status | TaxID (masked) | CreatedAt |
|----|------|------|---------|------|---------|--------|----------------|-----------|
| `wh-001` | Madurai Main Warehouse | WH-MAD | 123 Industrial Estate | Madurai | India | Active | XXXX1234 | 2026-01-01 |
| `wh-002` | Chennai Distribution Hub | WH-CHN | 45 Port Road | Chennai | India | Active | XXXX5678 | 2026-01-15 |
| `wh-003` | Mumbai Central Depot | WH-MUM | 78 Midtown Plaza | Mumbai | India | Active | XXXX9012 | 2026-02-01 |

#### WAREHOUSE ZONES

| Id | WarehouseId | Name | Code | ZoneType | IsCapacityEnforced | CreatedAt |
|----|-------------|------|------|----------|-------------------|-----------|
| `z-001` | wh-001 | Electronics Zone | Z-ELEC | Storage | true | 2026-01-01 |
| `z-002` | wh-001 | Receiving Zone | Z-RCV | Receiving | true | 2026-01-01 |
| `z-003` | wh-001 | Shipping Zone | Z-SHIP | Shipping | true | 2026-01-01 |
| `z-004` | wh-001 | Quality Inspection | Z-QC | Quality | true | 2026-01-01 |
| `z-005` | wh-002 | General Storage | Z-GEN | Storage | true | 2026-01-15 |
| `z-006` | wh-002 | Cold Storage | Z-COLD | Storage | true | 2026-01-15 |
| `z-007` | wh-003 | Overflow Storage | Z-OVR | Overflow | false | 2026-02-01 |

#### BIN LOCATIONS (BinCode Structure)

| Id | ZoneId | BinCode | Barcode | MaxVolumeCm3 | MaxWeightKg | BinType | CreatedAt |
|----|--------|-------|------|-----|---------|--------------|-------------|---------|-----------|
| `b-001` | z-001 | A01 | R01 | B01 | WH-MAD/E/A01/R01/B01 | 50000 | 50 | Standard | 2026-01-01 |
| `b-002` | z-001 | A01 | R01 | B02 | WH-MAD/E/A01/R01/B02 | 50000 | 50 | Standard | 2026-01-01 |
| `b-003` | z-001 | A01 | R02 | B01 | WH-MAD/E/A01/R02/B01 | 75000 | 75 | Bulk | 2026-01-01 |
| `b-004` | z-002 | A01 | R01 | B01 | WH-MAD/R/A01/R01/B01 | 100000 | 100 | Standard | 2026-01-01 |
| `b-005` | z-002 | A01 | R02 | B01 | WH-MAD/R/A01/R02/B01 | 100000 | 100 | Standard | 2026-01-01 |
| `b-006` | z-003 | A01 | R01 | B01 | WH-MAD/S/A01/R01/B01 | 60000 | 60 | Standard | 2026-01-01 |
| `b-007` | z-003 | A01 | R02 | B01 | WH-MAD/S/A01/R02/B01 | 60000 | 60 | Standard | 2026-01-01 |
| `b-008` | z-004 | A01 | R01 | B01 | WH-MAD/QC/A01/R01/B01 | 40000 | 40 | Standard | 2026-01-01 |
| `b-009` | z-005 | A02 | R03 | B05 | WH-CHN/G/A02/R03/B05 | 80000 | 80 | Standard | 2026-01-15 |
| `b-010` | z-006 | A01 | R01 | B01 | WH-CHN/C/A01/R01/B01 | 30000 | 30 | ColdStorage | 2026-01-15 |
| `b-011` | z-007 | A01 | R01 | B01 | WH-MUM/O/A01/R01/B01 | 120000 | 120 | Bulk | 2026-02-01 |

#### CATEGORIES (Hierarchical)

| Id | Name | Code | ParentId | Description | CreatedAt |
|----|------|------|----------|-------------|-----------|
| `cat-001` | Electronics | CAT-ELEC | - | Electronic devices and components | 2026-01-01 |
| `cat-002` | Computing | CAT-COMP | cat-001 | Laptops, desktops, accessories | 2026-01-01 |
| `cat-003` | Mobile Devices | CAT-MOBILE | cat-001 | Smartphones, tablets, wearables | 2026-01-01 |
| `cat-004` | Home Appliances | CAT-HOME | cat-001 | Kitchen, cleaning, HVAC appliances | 2026-01-01 |
| `cat-005` | Office Supplies | CAT-OFFICE | - | Stationery, furniture, supplies | 2026-01-01 |
| `cat-006` | Packaging | CAT-PACK | - | Packaging materials and supplies | 2026-01-01 |
| `cat-007` | Raw Materials | CAT-RAW | - | Components for manufacturing | 2026-01-01 |

#### SUPPLIERS

| Id | Name | Code | ContactPerson | Email | Phone | Address | LeadTimeDays | PaymentTerms | Rating | Status | RegistrationSource | CreatedAt |
|----|------|------|---------------|-------|-------|---------|--------------|--------------|--------|--------|-------------------|-----------|
| `sup-001` | ABC Technologies Pvt Ltd | SUP-ABC | Raj Kumar | sales@abctech.com | +919876543210 | 45 IT Park | 7 | Net30 | 4.5 | Active | SelfRegistered | 2026-01-01 |
| `sup-002` | Global Electronics Ltd | SUP-GE | Priya Sharma | orders@gelectro.com | +919876543211 | 78 Business District | 14 | Net60 | 4.2 | Active | SelfRegistered | 2026-01-10 |
| `sup-003` | Metro Supplies Co | SUP-METRO | Vijay Mehta | contact@metrosupplies.com | +919876543212 | 123 Commercial St | 3 | Net15 | 3.8 | Active | Invited | 2026-01-15 |
| `sup-004` | Premium Parts Inc | SUP-PREC | Anil Gupta | info@premiumparts.com | +919876543213 | 67 Industry Road | 10 | Net45 | 4.7 | Active | SelfRegistered | 2026-02-01 |
| `sup-005` | QuickSource Trading | SUP-QUICK | Meena Patel | sales@quicksourcetrading.com | +919876543214 | 89 Warehouse Area | 5 | Net30 | 4.0 | PendingReview | SelfRegistered | 2026-02-10 |

#### SUPPLIER CONTACTS (For Supplier Portal Users)

| Id | SupplierId | FullName | Email | PhoneNumber | JobTitle | CreatedAt |
|----|--------------|----------|-------|-----------|----------|-----------|
| `sc-001` | sup-001 | Raj Kumar | raj@abctech.com | +919876543210 | Sales Manager | 2026-01-01 |
| `sc-002` | sup-002 | Priya Sharma | priya@gelectro.com | +919876543211 | Procurement | 2026-01-10 |
| `sc-003` | sup-003 | Vijay Mehta | vijay@metrosupplies.com | +919876543212 | Operations | 2026-01-15 |
| `sc-004` | sup-004 | Anil Gupta | anil@premiumparts.com | +919876543213 | Logistics | 2026-02-01 |
| `sc-005` | sup-005 | Meena Patel | meena@quicksourcetrading.com | +919876543214 | Manager | 2026-02-10 |

#### USERS (Internal System Users)

| Id | FullName | Email | Role | WarehouseId | Status | CreatedAt |
|----|----------|-------|------|-------------|--------|-----------|
| `u-001` | Raj Kumar | raj.kumar@company.com | Manager | wh-001 | Active | 2026-01-01 |
| `u-002` | Priya Sharma | priya.sharma@company.com | Manager | wh-002 | Active | 2026-01-01 |
| `u-003` | Vijay Mehta | vijay.mehta@company.com | Staff | wh-001 | Active | 2026-01-01 |
| `u-004` | Anil Gupta | anil.gupta@company.com | Staff | wh-002 | Active | 2026-01-01 |
| `u-005` | Meena Patel | meena.patel@company.com | Viewer | - | Active | 2026-01-01 |
| `u-006` | Arjun Singh | arjun.singh@company.com | Staff | wh-003 | Active | 2026-02-01 |
| `u-007` | Suresh Kumar | suresh.kumar@company.com | Manager | wh-003 | Active | 2026-02-01 |

#### USER WAREHOUSE ACCESS

| Id | UserId | WarehouseId | AccessLevel | CreatedAt |
|----|--------|-------------|-------------|-----------|
| `uw-001` | u-001 | wh-001 | Full | 2026-01-01 |
| `uw-002` | u-002 | wh-002 | Full | 2026-01-01 |
| `uw-003` | u-003 | wh-001 | Full | 2026-01-01 |
| `uw-004` | u-004 | wh-002 | Full | 2026-01-01 |
| `uw-005` | u-005 | wh-001 | Read | 2026-01-01 |
| `uw-006` | u-005 | wh-002 | Read | 2026-01-01 |
| `uw-007` | u-006 | wh-003 | Full | 2026-02-01 |
| `uw-008` | u-007 | wh-003 | Full | 2026-02-01 |

#### PRODUCTS

| Id | Name | SKU | CategoryId | CostPrice | SellingPrice | UnitOfMeasure | SafetyStockQty | ReorderPoint | WeightKg | VolumeCm3 | PreferredBinType | CreatedAt |
|----|------|-----|------------|-----------|--------------|---------------|----------------|--------------|----------|-----------|------------------|-----------|
| `prod-001` | Dell Latitude 5450 Laptop | PRD-DELL-5450 | cat-002 | 45000.00 | 55000.00 | EACH | 10 | 20 | 2.0 | 15000 | Standard | 2026-01-01 |
| `prod-002` | HP EliteBook 840 Laptop | PRD-HP-840 | cat-002 | 42000.00 | 52000.00 | EACH | 8 | 15 | 1.8 | 12000 | Standard | 2026-01-01 |
| `prod-003` | Samsung Galaxy S24 | PRD-SAM-S24 | cat-003 | 38000.00 | 48000.00 | EACH | 15 | 30 | 0.2 | 800 | Standard | 2026-01-01 |
| `prod-004` | iPhone 15 Pro Max | PRD-IP-15PM | cat-003 | 125000.00 | 145000.00 | EACH | 5 | 10 | 0.22 | 900 | Standard | 2026-01-01 |
| `prod-005` | LG Refrigerator 400L | PRD-LG-REF | cat-004 | 35000.00 | 45000.00 | EACH | 3 | 5 | 75.0 | 250000 | Bulk | 2026-01-01 |
| `prod-006` | Philips Air Fryer | PRD-PHIL-AF | cat-004 | 3500.00 | 5500.00 | EACH | 20 | 40 | 4.5 | 15000 | Standard | 2026-01-01 |
| `prod-007` | Office Chair Executive | PRD-OFF-CHAIR | cat-005 | 4500.00 | 7500.00 | EACH | 10 | 20 | 12.0 | 30000 | Standard | 2026-01-01 |
| `prod-008` | A4 Paper Ream (500 sheets) | PRD-PAPER-500 | cat-005 | 250.00 | 350.00 | REAM | 100 | 200 | 2.5 | 10000 | Standard | 2026-01-01 |
| `prod-009` | Packaging Bubble Wrap (100m) | PRD-BUB-100M | cat-006 | 800.00 | 1200.00 | ROLL | 50 | 100 | 1.5 | 5000 | Standard | 2026-01-01 |
| `prod-010` | Corrugated Box Small | PRD-CBX-S | cat-006 | 45.00 | 75.00 | EACH | 200 | 400 | 0.2 | 3000 | Standard | 2026-01-01 |
| `prod-011` | Microcontroller ESP32 | PRD-ESP32 | cat-007 | 800.00 | 1200.00 | EACH | 50 | 100 | 0.05 | 200 | Standard | 2026-01-01 |
| `prod-012` | PCB Board 10x15cm | PRD-PCB-1015 | cat-007 | 150.00 | 250.00 | EACH | 30 | 60 | 0.1 | 500 | Standard | 2026-01-01 |

#### SUPPLIER PRODUCTS (Supplier Catalogue)

| Id | SupplierId | ProductId | UnitPrice | MinOrderQuantity | IsActive | CreatedAt |
|----|------------|-----------|-----------|------------------|----------|-----------|
| `sp-001` | sup-001 | prod-001 | 44000.00 | 10 | true | 2026-01-01 |
| `sp-002` | sup-001 | prod-002 | 41000.00 | 5 | true | 2026-01-01 |
| `sp-003` | sup-001 | prod-007 | 4200.00 | 20 | true | 2026-01-01 |
| `sp-004` | sup-001 | prod-011 | 750.00 | 50 | true | 2026-01-01 |
| `sp-005` | sup-002 | prod-003 | 37000.00 | 15 | true | 2026-01-10 |
| `sp-006` | sup-002 | prod-004 | 120000.00 | 5 | true | 2026-01-10 |
| `sp-007` | sup-002 | prod-010 | 40.00 | 100 | true | 2026-01-10 |
| `sp-008` | sup-003 | prod-005 | 34000.00 | 3 | true | 2026-01-15 |
| `sp-009` | sup-003 | prod-006 | 3200.00 | 10 | true | 2026-01-15 |
| `sp-010` | sup-003 | prod-008 | 220.00 | 50 | true | 2026-01-15 |
| `sp-011` | sup-004 | prod-009 | 750.00 | 25 | true | 2026-02-01 |
| `sp-012` | sup-004 | prod-012 | 130.00 | 30 | true | 2026-02-01 |
| `sp-013` | sup-005 | prod-001 | 43500.00 | 20 | true | 2026-02-10 |

#### STOCK LEVELS (Initial Inventory)

| Id | ProductId | WarehouseId | BinLocationId | QuantityOnHand | QuantityReserved | QuantityOnOrder | QuantityInTransit | LastUpdated |
|----|-----------|-------------|---------------|----------------|------------------|-----------------|-------------------|-------------|
| `sl-001` | prod-001 | wh-001 | b-001 | 50 | 0 | 0 | 0 | 2026-01-01 |
| `sl-002` | prod-002 | wh-001 | b-002 | 35 | 0 | 0 | 0 | 2026-01-01 |
| `sl-003` | prod-003 | wh-001 | b-001 | 45 | 0 | 0 | 0 | 2026-01-01 |
| `sl-004` | prod-004 | wh-001 | b-003 | 20 | 0 | 0 | 0 | 2026-01-01 |
| `sl-005` | prod-005 | wh-001 | b-003 | 10 | 0 | 0 | 0 | 2026-01-01 |
| `sl-006` | prod-006 | wh-001 | b-004 | 60 | 0 | 0 | 0 | 2026-01-01 |
| `sl-007` | prod-007 | wh-001 | b-005 | 25 | 0 | 0 | 0 | 2026-01-01 |
| `sl-008` | prod-008 | wh-001 | b-006 | 200 | 0 | 0 | 0 | 2026-01-01 |
| `sl-009` | prod-009 | wh-001 | b-007 | 80 | 0 | 0 | 0 | 2026-01-01 |
| `sl-010` | prod-010 | wh-001 | b-007 | 300 | 0 | 0 | 0 | 2026-01-01 |
| `sl-011` | prod-011 | wh-001 | b-001 | 100 | 0 | 0 | 0 | 2026-01-01 |
| `sl-012` | prod-012 | wh-001 | b-001 | 75 | 0 | 0 | 0 | 2026-01-01 |
| `sl-013` | prod-001 | wh-002 | b-009 | 25 | 0 | 0 | 0 | 2026-01-15 |
| `sl-014` | prod-005 | wh-002 | b-010 | 15 | 0 | 0 | 0 | 2026-01-15 |
| `sl-015` | prod-010 | wh-002 | b-009 | 150 | 0 | 0 | 0 | 2026-01-15 |
| `sl-016` | prod-006 | wh-003 | b-011 | 40 | 0 | 0 | 0 | 2026-02-01 |
| `sl-017` | prod-007 | wh-003 | b-011 | 30 | 0 | 0 | 0 | 2026-02-01 |
| `sl-018` | prod-008 | wh-003 | b-011 | 180 | 0 | 0 | 0 | 2026-02-01 |
| `sl-019` | prod-009 | wh-003 | b-011 | 60 | 0 | 0 | 0 | 2026-02-01 |
| `sl-020` | prod-010 | wh-003 | b-011 | 250 | 0 | 0 | 0 | 2026-02-01 |

#### STOCK MOVEMENTS (Initial Record)

| Id | ProductId | WarehouseId | BinLocationId | MovementType | Quantity | ReferenceType | ReferenceId | PerformedBy | CreatedAt |
|----|-----------|-------------|---------------|--------------|----------|---------------|-------------|-------------|-----------|
| `sm-001` | prod-001 | wh-001 | b-001 | Purchase | 50 | PurchaseOrder | grn-001 | u-003 | 2026-01-01 |
| `sm-002` | prod-002 | wh-001 | b-002 | Purchase | 35 | PurchaseOrder | grn-001 | u-003 | 2026-01-01 |
| `sm-003` | prod-003 | wh-001 | b-001 | Purchase | 45 | PurchaseOrder | grn-001 | u-003 | 2026-01-01 |
| `sm-004` | prod-004 | wh-001 | b-003 | Purchase | 20 | PurchaseOrder | grn-001 | u-003 | 2026-01-01 |
| `sm-005` | prod-005 | wh-001 | b-003 | Purchase | 10 | PurchaseOrder | grn-001 | u-003 | 2026-01-01 |
| `sm-006` | prod-006 | wh-001 | b-004 | Purchase | 60 | PurchaseOrder | grn-001 | u-003 | 2026-01-01 |
| `sm-007` | prod-007 | wh-001 | b-005 | Purchase | 25 | PurchaseOrder | grn-001 | u-003 | 2026-01-01 |
| `sm-008` | prod-008 | wh-001 | b-006 | Purchase | 200 | PurchaseOrder | grn-001 | u-003 | 2026-01-01 |
| `sm-009` | prod-009 | wh-001 | b-007 | Purchase | 80 | PurchaseOrder | grn-001 | u-003 | 2026-01-01 |
| `sm-010` | prod-010 | wh-001 | b-007 | Purchase | 300 | PurchaseOrder | grn-001 | u-003 | 2026-01-01 |
| `sm-011` | prod-011 | wh-001 | b-001 | Purchase | 100 | PurchaseOrder | grn-001 | u-003 | 2026-01-01 |
| `sm-012` | prod-012 | wh-001 | b-001 | Purchase | 75 | PurchaseOrder | grn-001 | u-003 | 2026-01-01 |

#### BARCODES

| Id | ProductId | BarcodeValue | BarcodeType | IsPrimary | CreatedAt |
|----|-----------|--------------|-------------|-----------|-----------|
| `bc-001` | prod-001 | 1234567890123 | Code128 | true | 2026-01-01 |
| `bc-002` | prod-001 | QRP-DELL-5450 | QRCode | false | 2026-01-01 |
| `bc-003` | prod-002 | 1234567890124 | Code128 | true | 2026-01-01 |
| `bc-004` | prod-003 | 1234567890125 | Code128 | true | 2026-01-01 |
| `bc-005` | prod-004 | 1234567890126 | Code128 | true | 2026-01-01 |
| `bc-006` | prod-005 | 1234567890127 | Code128 | true | 2026-01-01 |
| `bc-007` | prod-006 | 1234567890128 | Code128 | true | 2026-01-01 |
| `bc-008` | prod-007 | 1234567890129 | Code128 | true | 2026-01-01 |
| `bc-009` | prod-008 | 1234567890130 | Code128 | true | 2026-01-01 |
| `bc-010` | prod-009 | 1234567890131 | Code128 | true | 2026-01-01 |
| `bc-011` | prod-010 | 1234567890132 | Code128 | true | 2026-01-01 |
| `bc-012` | prod-011 | 1234567890133 | Code128 | true | 2026-01-01 |
| `bc-013` | prod-012 | 1234567890134 | Code128 | true | 2026-01-01 |

#### BARCODE SCAN LOGS

| Id | BarcodeId | ScannedBy | WarehouseId | Action | ScannedAt | CreatedAt |
|----|-----------|-----------|-------------|--------|-----------|-----------|
| `bsl-001` | bc-001 | u-003 | wh-001 | InventoryCount | 2026-01-01 10:30:00 | 2026-01-01 10:30:00 |
| `bsl-002` | bc-002 | u-003 | wh-001 | InventoryCount | 2026-01-01 10:31:00 | 2026-01-01 10:31:00 |
| `bsl-003` | bc-003 | u-003 | wh-001 | Putaway | 2026-01-01 11:00:00 | 2026-01-01 11:00:00 |

#### FILE ATTACHMENTS (Initial)

| Id | EntityId | EntityType | FileName | MimeType | FileSize | Category | UploadedBy | CreatedAt |
|----|----------|------------|----------|----------|----------|----------|------------|-----------|
| `fa-001` | grn-001 | GoodsReceipt | DeliveryChallan_001.pdf | application/pdf | 245678 | DeliveryChallan | u-003 | 2026-01-01 |
| `fa-002` | grn-001 | GoodsReceipt | PackingList_001.pdf | application/pdf | 123456 | PackingList | u-003 | 2026-01-01 |

---

## PHASE 3 — COMPLETE BUSINESS FLOW

### 3.1 ADMIN LOGIN

**API Endpoint**: `POST /api/v1/Auth/signin`

**Request Payload**:
```json
{
  "email": "admin@smartinventory.com",
  "password": "Admin@123"
}
```

**Response Payload**:
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "abc123refresh...",
  "expiresIn": 3600,
  "user": {
    "id": "b0d33b91-4567-4eef-b123-888888888801",
    "fullName": "System Administrator",
    "email": "admin@smartinventory.com",
    "role": "Admin",
    "permissions": ["Admin", "Manage", "Inventory", "View"]
  }
}
```

**Tables Updated**: None (authentication only)

**Audit Entries Created**: None (login is not audited by default)

**Notifications Created**: None

**Outbox Messages Created**: None

**Database State Before/After**:
- Before: User session not established
- After: JWT token issued for admin user

---

### 3.2 CREATE WAREHOUSE STRUCTURE

**API Endpoint**: `POST /api/v1/Warehouses`

**Request Payload**:
```json
{
  "name": "Madurai Main Warehouse",
  "code": "WH-MAD",
  "address": "123 Industrial Estate",
  "city": "Madurai",
  "country": "India",
  "taxIdentifier": "33ABCDE1234F1Z5",
  "registrationNumber": "U74100TN2026PTC123456",
  "contactPerson": "Raj Kumar",
  "email": "madurai@smartinventory.com",
  "phone": "+919876543210",
  "shareContactDetails": true
}
```

**Response Payload**:
```json
{
  "id": "wh-001",
  "name": "Madurai Main Warehouse",
  "code": "WH-MAD",
  "address": "123 Industrial Estate",
  "city": "Madurai",
  "country": "India",
  "status": "PendingVerification",
  "taxIdentifierLastFour": "1234",
  "registrationNumberLastFour": "2345",
  "createdAt": "2026-01-01T00:00:00Z"
}
```

**Tables Updated**: `Warehouses`, `Users` (Manager reference)

**Audit Entries Created**:
```json
{
  "EntityType": "Warehouse",
  "EntityId": "wh-001",
  "Action": "Create",
  "OldValues": null,
  "NewValues": {
    "Id": "wh-001",
    "Name": "Madurai Main Warehouse",
    "Code": "WH-MAD",
    "Address": "123 Industrial Estate",
    "Status": "PendingVerification"
  },
  "UserId": "admin-user-id",
  "IpAddress": "192.168.1.100"
}
```

**Notifications Created**: None

**Outbox Messages Created**: None

---

### 3.3 CREATE ZONES AND BINS

**API Endpoint**: `POST /api/v1/Warehouses/{wh-001}/zones`

**Request Payload**:
```json
{
  "name": "Electronics Zone",
  "code": "Z-ELEC",
  "zoneType": "Storage",
  "isCapacityEnforced": true
}
```

**Response Payload**:
```json
{
  "id": "z-001",
  "warehouseId": "wh-001",
  "name": "Electronics Zone",
  "code": "Z-ELEC",
  "zoneType": "Storage",
  "isCapacityEnforced": true,
  "createdAt": "2026-01-01T00:00:00Z"
}
```

**API Endpoint**: `POST /api/v1/Warehouses/zones/{z-001}/bins`

**Request Payload**:
```json
{
  "binCode": "B01",
  "barcode": "Z-ELEC-B01",
  "maxVolumeCm3": 50000,
  "maxWeightKg": 50,
  "binType": "Standard"
}
```

**Response Payload**:
```json
{
  "id": "b-001",
  "zoneId": "z-001",
  "zoneName": "Electronics Zone",
  "binCode": "B01",
  "barcode": "Z-ELEC-B01",
  "maxVolumeCm3": 50000,
  "maxWeightKg": 50,
  "binType": "Standard",
  "isActive": true,
  "createdAt": "2026-01-01T00:00:00Z"
}
```

**Tables Updated**: `WarehouseZones`, `BinLocations`

**Audit Entries Created**: Yes (for both entities)

---

### 3.4 CREATE CATEGORIES

**API Endpoint**: `POST /api/v1/Categories`

**Request Payload**:
```json
{
  "name": "Electronics",
  "code": "CAT-ELEC",
  "description": "Electronic devices and components"
}
```

**Response Payload**:
```json
{
  "id": "cat-001",
  "name": "Electronics",
  "code": "CAT-ELEC",
  "description": "Electronic devices and components",
  "createdAt": "2026-01-01T00:00:00Z"
}
```

**Subcategories**:
```json
{
  "name": "Computing",
  "code": "CAT-COMP",
  "parentId": "cat-001",
  "description": "Laptops, desktops, accessories"
}
```

**Tables Updated**: `Categories`

**Audit Entries Created**: Yes

---

### 3.5 CREATE PRODUCTS

**API Endpoint**: `POST /api/v1/Products`

**Request Payload**:
```json
{
  "name": "Dell Latitude 5450 Laptop",
  "sku": "PRD-DELL-5450",
  "categoryId": "cat-002",
  "costPrice": 45000.00,
  "sellingPrice": 55000.00,
  "unitOfMeasure": "EACH",
  "reorderPoint": 20,
  "reorderQuantity": 50,
  "safetyStockQty": 10,
  "length": 35.0,
  "width": 25.0,
  "height": 3.0,
  "weightKg": 2.0,
  "preferredBinType": "Standard"
}
```

**Response Payload**:
```json
{
  "id": "prod-001",
  "name": "Dell Latitude 5450 Laptop",
  "sku": "PRD-DELL-5450",
  "categoryId": "cat-002",
  "categoryName": "Computing",
  "costPrice": 45000.00,
  "sellingPrice": 55000.00,
  "unitOfMeasure": "EACH",
  "reorderPoint": 20,
  "reorderQuantity": 50,
  "safetyStockQty": 10,
  "volumeCm3": 2625.0,
  "weightKg": 2.0,
  "preferredBinType": "Standard",
  "createdAt": "2026-01-01T00:00:00Z"
}
```

**Tables Updated**: `Products`, `Products` (ABC classification computed after inventory data)

**Audit Entries Created**: Yes

**Notifications Created**: None

**Outbox Messages Created**: None

---

### 3.6 APPROVE SUPPLIER

**API Endpoint**: `POST /api/v1/Suppliers`

**Request Payload**:
```json
{
  "name": "ABC Technologies Pvt Ltd",
  "code": "SUP-ABC",
  "contactPerson": "Raj Kumar",
  "email": "sales@abctech.com",
  "phone": "+919876543210",
  "address": "45 IT Park, Madurai",
  "leadTimeDays": 7,
  "paymentTerms": "Net30",
  "creditLimit": 500000.00
}
```

**Response Payload**:
```json
{
  "id": "sup-001",
  "name": "ABC Technologies Pvt Ltd",
  "code": "SUP-ABC",
  "contactPerson": "Raj Kumar",
  "email": "sales@abctech.com",
  "leadTimeDays": 7,
  "paymentTerms": "Net30",
  "creditLimit": 500000.00,
  "rating": 0.0,
  "status": "Registered",
  "createdAt": "2026-01-01T00:00:00Z"
}
```

**API Endpoint**: `POST /api/v1/Suppliers/{sup-001}/review`

**Request Payload**:
```json
{
  "action": "Approve",
  "message": "Verified credentials and documents. Approved for business."
}
```

**Response Payload**:
```json
{
  "id": "sup-001",
  "name": "ABC Technologies Pvt Ltd",
  "code": "SUP-ABC",
  "status": "Active",
  "agreementSignedAt": "2026-01-02T10:30:00Z",
  "agreementSignedIp": "192.168.1.100"
}
```

**Tables Updated**: `Suppliers`, `SupplierContacts`

**Audit Entries Created**: Yes (for Supplier status change)

**Notifications Created**:
- Supplier gets notified: "Your registration has been approved"

---

### 3.7 ADD SUPPLIER PRODUCTS

**API Endpoint**: `POST /api/v1/Suppliers/{sup-001}/products`

**Request Payload**:
```json
{
  "productId": "prod-001",
  "unitPrice": 44000.00,
  "minOrderQuantity": 10
}
```

**Response Payload**:
```json
{
  "id": "sp-001",
  "supplierId": "sup-001",
  "productId": "prod-001",
  "productName": "Dell Latitude 5450 Laptop",
  "productSku": "PRD-DELL-5450",
  "unitPrice": 44000.00,
  "minOrderQuantity": 10,
  "isActive": true,
  "createdAt": "2026-01-01T00:00:00Z"
}
```

**Tables Updated**: `SupplierProducts`

**Audit Entries Created**: Yes

---

### 3.8 CREATE PURCHASE ORDER

**API Endpoint**: `POST /api/v1/PurchaseOrders`

**Request Payload**:
```json
{
  "supplierId": "sup-001",
  "warehouseId": "wh-001",
  "expectedDelivery": "2026-01-08T00:00:00Z",
  "notes": "Regular monthly replenishment order for electronics stock.",
  "idempotencyKey": "PO-2026-01-001",
  "items": [
    {
      "productId": "prod-001",
      "quantityOrdered": 50,
      "unitPrice": 44000.00
    },
    {
      "productId": "prod-002",
      "quantityOrdered": 30,
      "unitPrice": 41000.00
    },
    {
      "productId": "prod-003",
      "quantityOrdered": 40,
      "unitPrice": 37000.00
    }
  ]
}
```

**Response Payload**:
```json
{
  "id": "po-001",
  "poNumber": "PO-2026-001",
  "supplierId": "sup-001",
  "supplierName": "ABC Technologies Pvt Ltd",
  "warehouseId": "wh-001",
  "warehouseName": "Madurai Main Warehouse",
  "status": "Draft",
  "totalAmount": 4410000.00,
  "expectedDelivery": "2026-01-08T00:00:00Z",
  "createdAt": "2026-01-02T14:30:00Z",
  "items": [
    {
      "id": "poi-001",
      "productId": "prod-001",
      "productName": "Dell Latitude 5450 Laptop",
      "quantityOrdered": 50,
      "quantityReceived": 0,
      "unitPrice": 44000.00,
      "totalPrice": 2200000.00
    },
    {
      "id": "poi-002",
      "productId": "prod-002",
      "productName": "HP EliteBook 840 Laptop",
      "quantityOrdered": 30,
      "quantityReceived": 0,
      "unitPrice": 41000.00,
      "totalPrice": 1230000.00
    },
    {
      "id": "poi-003",
      "productId": "prod-003",
      "productName": "Samsung Galaxy S24",
      "quantityOrdered": 40,
      "quantityReceived": 0,
      "unitPrice": 37000.00,
      "totalPrice": 1480000.00
    }
  ]
}
```

**Tables Updated**: `PurchaseOrders`, `PurchaseOrderItems`

**Audit Entries Created**: Yes

**Notifications Created**:
- In-app notification to warehouse manager: "New Purchase Order created"

---

### 3.9 SUPPLIER ACCEPTS PO

**API Endpoint**: `POST /api/v1/supplier/purchase-orders/{po-001}/respond`

**Request Payload**:
```json
{
  "accept": true,
  "expectedDeliveryDate": "2026-01-07T00:00:00Z"
}
```

**Response Payload**:
```json
{
  "message": "Purchase order accepted."
}
```

**Tables Updated**: `PurchaseOrders` (SupplierAccepted = true, SupplierCommittedDeliveryDate updated)

**Audit Entries Created**: Yes

**Notifications Created**: None (internal notification would be sent)

---

### 3.10 SUBMIT PO FOR APPROVAL

**API Endpoint**: `PUT /api/v1/PurchaseOrders/{po-001}/submit`

**Request Payload**: None

**Response Payload**:
```json
{
  "id": "po-001",
  "poNumber": "PO-2026-001",
  "status": "Submitted",
  "totalAmount": 4410000.00
}
```

**Tables Updated**: `PurchaseOrders` (Status = Submitted)

**Audit Entries Created**: Yes

---

### 3.11 APPROVE PURCHASE ORDER

**API Endpoint**: `PUT /api/v1/PurchaseOrders/{po-001}/approve`

**Request Payload**:
```json
{
  "approve": true,
  "notes": "Approved for delivery."
}
```

**Response Payload**:
```json
{
  "id": "po-001",
  "poNumber": "PO-2026-001",
  "status": "Approved",
  "totalAmount": 4410000.00,
  "approvedBy": "admin-user-id",
  "approvedByName": "System Administrator"
}
```

**Tables Updated**: `PurchaseOrders` (Status = Approved, ApprovedBy updated)

**Audit Entries Created**: Yes

**Notifications Created**:
- In-app to supplier contact: "Purchase Order PO-2026-001 has been approved"
- In-app to PO creator: "Your PO has been approved"

**MediatR Event Published**: `PurchaseOrderApprovedEvent`

---

### 3.12 GOODS RECEIVED

**API Endpoint**: `POST /api/v1/PurchaseOrders/{po-001}/grn`

**Request Payload**:
```json
{
  "warehouseId": "wh-001",
  "receivedBy": "u-003",
  "notes": "Received goods as per delivery Challan.",
  "idempotencyKey": "GRN-2026-01-001",
  "attachmentIds": ["fa-001", "fa-002"],
  "bypassWarnings": false,
  "items": [
    {
      "purchaseOrderItemId": "poi-001",
      "quantityReceived": 50,
      "quantityRejected": 0,
      "binLocationId": "b-001",
      "rejectionReason": null
    },
    {
      "purchaseOrderItemId": "poi-002",
      "quantityReceived": 30,
      "quantityRejected": 0,
      "binLocationId": "b-002",
      "rejectionReason": null
    },
    {
      "purchaseOrderItemId": "poi-003",
      "quantityReceived": 40,
      "quantityRejected": 0,
      "binLocationId": "b-001",
      "rejectionReason": null
    }
  ]
}
```

**Response Payload**:
```json
{
  "id": "grn-001",
  "grnNumber": "GRN-2026-001",
  "purchaseOrderId": "po-001",
  "purchaseOrderNumber": "PO-2026-001",
  "warehouseId": "wh-001",
  "warehouseName": "Madurai Main Warehouse",
  "receivedBy": "u-003",
  "receivedByUserName": "Vijay Mehta",
  "receivedDate": "2026-01-07T15:30:00Z",
  "status": "Accepted",
  "createdAt": "2026-01-07T15:30:00Z",
  "items": [
    {
      "id": "gri-001",
      "purchaseOrderItemId": "poi-001",
      "productId": "prod-001",
      "productName": "Dell Latitude 5450 Laptop",
      "quantityReceived": 50,
      "quantityRejected": 0,
      "binLocationId": "b-001",
      "rejectionReason": null
    },
    {
      "id": "gri-002",
      "purchaseOrderItemId": "poi-002",
      "productId": "prod-002",
      "productName": "HP EliteBook 840 Laptop",
      "quantityReceived": 30,
      "quantityRejected": 0,
      "binLocationId": "b-002",
      "rejectionReason": null
    },
    {
      "id": "gri-003",
      "purchaseOrderItemId": "poi-003",
      "productId": "prod-003",
      "productName": "Samsung Galaxy S24",
      "quantityReceived": 40,
      "quantityRejected": 0,
      "binLocationId": "b-001",
      "rejectionReason": null
    }
  ]
}
```

**Tables Updated**: `GoodsReceipts`, `GoodsReceiptItems`, `PurchaseOrderItems`, `StockLevels`, `StockMovements`

**Stock Level Updates**:
| ProductId | WarehouseId | BinLocationId | QuantityOnHand | LastUpdated |
|-----------|-------------|---------------|----------------|-------------|
| prod-001 | wh-001 | b-001 | 100 | 2026-01-07 15:30:00 |
| prod-002 | wh-001 | b-002 | 65 | 2026-01-07 15:30:00 |
| prod-003 | wh-001 | b-001 | 85 | 2026-01-07 15:30:00 |

**Stock Movement Records Created**:
| MovementType | ProductId | WarehouseId | BinLocationId | Quantity | ReferenceId |
|--------------|-----------|-------------|---------------|----------|-------------|
| Purchase | prod-001 | wh-001 | b-001 | 50 | grn-001 |
| Purchase | prod-002 | wh-001 | b-002 | 30 | grn-001 |
| Purchase | prod-003 | wh-001 | b-001 | 40 | grn-001 |

**Audit Entries Created**: Yes (for all entities)

**Notifications Created**:
- In-app to PO creator: "Goods Receipt GRN-2026-001 has been recorded for PO PO-2026-001"

**Outbox Messages Created**: `OutboxMessage` with EventType `StockLevelChanged` and `SendNotification`

---

### 3.13 BARCODE GENERATED

**API Endpoint**: `POST /api/v1/Barcodes/generate`

**Request Payload**:
```json
{
  "productId": "prod-001",
  "barcodeValue": "1234567890123",
  "type": "Code128",
  "isPrimary": true
}
```

**Response Payload**:
```json
{
  "id": "bc-001",
  "productId": "prod-001",
  "productName": "Dell Latitude 5450 Laptop",
  "productSKU": "PRD-DELL-5450",
  "barcodeValue": "1234567890123",
  "barcodeType": "Code128",
  "isPrimary": true,
  "createdAt": "2026-01-07T16:00:00Z"
}
```

**Tables Updated**: `Barcodes`

**Audit Entries Created**: Yes

**Notifications Created**: None

**Outbox Messages Created**: None

---

### 3.14 STOCK CREATED (After GRN)

**Stock Level State After GRN**:

| ProductId | WarehouseId | BinLocationId | QuantityOnHand | QuantityReserved | QuantityOnOrder | QuantityInTransit | LastUpdated |
|-----------|-------------|---------------|----------------|------------------|-----------------|-------------------|-------------|
| prod-001 | wh-001 | b-001 | 100 | 0 | 0 | 0 | 2026-01-07 15:30:00 |
| prod-002 | wh-001 | b-002 | 65 | 0 | 0 | 0 | 2026-01-07 15:30:00 |
| prod-003 | wh-001 | b-001 | 85 | 0 | 0 | 0 | 2026-01-07 15:30:00 |

**Calculation**: Previous QOH (50, 35, 45) + Received QOH (50, 30, 40) = 100, 65, 85

---

### 3.15 TRANSFER REQUESTED

**API Endpoint**: `POST /api/v1/Transfers`

**Request Payload**:
```json
{
  "fromWarehouseId": "wh-001",
  "toWarehouseId": "wh-002",
  "requestedBy": "u-001",
  "notes": "Transfer excess stock to Chennai distribution hub.",
  "idempotencyKey": "TRF-2026-001",
  "items": [
    {
      "productId": "prod-001",
      "fromBinId": "b-001",
      "toBinId": "b-009",
      "quantityRequested": 100
    }
  ]
}
```

**Response Payload**:
```json
{
  "id": "trf-001",
  "transferNumber": "TRF-2026-001",
  "fromWarehouseId": "wh-001",
  "fromWarehouseName": "Madurai Main Warehouse",
  "toWarehouseId": "wh-002",
  "toWarehouseName": "Chennai Distribution Hub",
  "requestedBy": "u-001",
  "requestedByName": "Raj Kumar",
  "status": "Requested",
  "transferDate": "2026-01-08T09:00:00Z",
  "createdAt": "2026-01-08T09:00:00Z",
  "items": [
    {
      "id": "tri-001",
      "productId": "prod-001",
      "productName": "Dell Latitude 5450 Laptop",
      "fromBinId": "b-001",
      "fromBinCode": "WH-MAD/E/A01/R01/B01",
      "toBinId": "b-009",
      "quantityRequested": 100,
      "quantityDispatched": 0,
      "quantityReceived": 0
    }
  ]
}
```

**Tables Updated**: `WarehouseTransfers`, `TransferItems`

**Stock Level Updates**:
- **Origin Warehouse (wh-001)**:
  - prod-001, b-001: QuantityReserved increased by 100 (QOH=100, Reserved=100, OOH=0)
- **Destination Warehouse (wh-002)**:
  - prod-001, b-009: QuantityInTransit = 100 (QOH=0, Reserved=0, OOH=100)

**Stock Movement Records Created**: None yet (reserved, not deducted)

**Audit Entries Created**: Yes

**Notifications Created**:
- In-app to destination manager: "Incoming Transfer Request TRF-2026-001 has been initiated from Madurai Main Warehouse."

**Outbox Messages Created**: None

---

### 3.16 TRANSFER APPROVED

**API Endpoint**: `PUT /api/v1/Transfers/{trf-001}/approve`

**Request Payload**:
```json
{
  "approve": true,
  "approvedBy": "u-002"
}
```

**Response Payload**:
```json
{
  "id": "trf-001",
  "transferNumber": "TRF-2026-001",
  "status": "Approved",
  "approvedBy": "u-002",
  "approvedByName": "Priya Sharma"
}
```

**Tables Updated**: `WarehouseTransfers` (Status = Approved)

**Stock Level Updates**: No change (stock still reserved)

**Stock Movement Records Created**: None

**Audit Entries Created**: Yes

**Notifications Created**:
- In-app to requester: "Your transfer request TRF-2026-001 has been approved by Priya Sharma."

---

### 3.17 TRANSFER DISPATCHED

**API Endpoint**: `PUT /api/v1/Transfers/{trf-001}/dispatch`

**Request Payload**: None

**Response Payload**:
```json
{
  "id": "trf-001",
  "transferNumber": "TRF-2026-001",
  "status": "InTransit",
  "items": [
    {
      "id": "tri-001",
      "productId": "prod-001",
      "quantityDispatched": 100,
      "quantityReceived": 0
    }
  ]
}
```

**Tables Updated**: `WarehouseTransfers` (Status = InTransit), `TransferItems` (QuantityDispatched = 100)

**Stock Level Updates**:

**Origin Warehouse (wh-001)** - prod-001, b-001:
| Field | Before | After | Change |
|-------|--------|-------|--------|
| QuantityOnHand | 100 | 0 | -100 (deducted) |
| QuantityReserved | 100 | 0 | -100 (released) |
| QuantityInTransit | 0 | 0 | No change |

**Destination Warehouse (wh-002)** - prod-001, b-009:
| Field | Before | After | Change |
|-------|--------|-------|--------|
| QuantityOnHand | 0 | 0 | No change |
| QuantityReserved | 0 | 0 | No change |
| QuantityInTransit | 0 | 100 | +100 (tracked) |

**Stock Movement Records Created**:
| MovementType | ProductId | WarehouseId | BinLocationId | Quantity | ReferenceId |
|--------------|-----------|-------------|---------------|----------|-------------|
| TransferOut | prod-001 | wh-001 | b-001 | 100 | trf-001 |
| TransferOut | prod-001 | wh-002 | b-009 | 100 | trf-001 (recorded in Transit) |

**Audit Entries Created**: Yes

**Notifications Created**:
- In-app to destination manager: "Transfer shipment TRF-2026-001 has been dispatched and is now in-transit."

---

### 3.18 TRANSFER RECEIVED

**API Endpoint**: `PUT /api/v1/Transfers/{trf-001}/receive`

**Request Payload**:
```json
{
  "items": [
    {
      "transferItemId": "tri-001",
      "quantityReceived": 100,
      "overrideReason": null
    }
  ]
}
```

**Response Payload**:
```json
{
  "id": "trf-001",
  "transferNumber": "TRF-2026-001",
  "status": "Received",
  "items": [
    {
      "id": "tri-001",
      "productId": "prod-001",
      "quantityDispatched": 100,
      "quantityReceived": 100
    }
  ]
}
```

**Tables Updated**: `WarehouseTransfers` (Status = Received), `TransferItems` (QuantityReceived = 100)

**Stock Level Updates**:

**Destination Warehouse (wh-002)** - prod-001, b-009:
| Field | Before | After | Change |
|-------|--------|-------|--------|
| QuantityOnHand | 0 | 100 | +100 (received) |
| QuantityReserved | 0 | 0 | No change |
| QuantityInTransit | 100 | 0 | -100 (cleared) |

**Stock Movement Records Created**:
| MovementType | ProductId | WarehouseId | BinLocationId | Quantity | ReferenceId |
|--------------|-----------|-------------|---------------|----------|-------------|
| TransferIn | prod-001 | wh-002 | b-009 | 100 | trf-001 |

**Audit Entries Created**: Yes

**Notifications Created**:
- In-app to requester: "Transfer shipment TRF-2026-001 has been received successfully at the destination facility."

---

### 3.19 STOCK ADJUSTMENT

**API Endpoint**: `POST /api/v1/StockAdjustments`

**Request Payload** (Manual adjustment):
```json
{
  "productId": "prod-001",
  "warehouseId": "wh-002",
  "binLocationId": "b-009",
  "reason": "Damage",
  "quantityAfter": 98,
  "notes": "Found 2 units damaged during unloading. Adjusting stock.",
  "idempotencyKey": "ADJ-2026-001"
}
```

**Response Payload**:
```json
{
  "id": "adj-001",
  "adjustmentNumber": "ADJ-2026-001",
  "productId": "prod-001",
  "productName": "Dell Latitude 5450 Laptop",
  "warehouseId": "wh-002",
  "warehouseName": "Chennai Distribution Hub",
  "reason": "Damage",
  "status": "Pending",
  "quantityBefore": 100,
  "quantityAfter": 98,
  "quantityChange": -2,
  "notes": "Found 2 units damaged during unloading. Adjusting stock.",
  "performedBy": "u-004",
  "performedByName": "Anil Gupta",
  "createdAt": "2026-01-08T15:00:00Z"
}
```

**Note**: Status = Pending because variance exceeds 5% threshold (2/100 = 2%, but if value variance > $100 triggers approval).

**Tables Updated**: `StockAdjustments`, `StockLevel`

**Stock Level Updates**:
- **If auto-approved**: prod-001, wh-002, b-009: QuantityOnHand = 98
- **If pending**: No change until approved

**Stock Movement Records Created** (if approved):
| MovementType | ProductId | WarehouseId | BinLocationId | Quantity | ReferenceId |
|--------------|-----------|-------------|---------------|----------|-------------|
| Adjustment | prod-001 | wh-002 | b-009 | 2 | adj-001 |

**Audit Entries Created**: Yes

**Notifications Created**:
- In-app to approver: "Stock Adjustment ADJ-2026-001 requires your approval."

---

### 3.20 INVENTORY SEARCH

**API Endpoint**: `POST /api/v1/Products/search`

**Request Payload**:
```json
{
  "filters": [
    {
      "field": "SKU",
      "operator": "contains",
      "value": "DELL"
    },
    {
      "field": "CategoryName",
      "operator": "eq",
      "value": "Computing"
    }
  ],
  "sort": [
    {
      "field": "SellingPrice",
      "direction": "desc"
    }
  ],
  "page": 1,
  "pageSize": 10
}
```

**Response Payload**:
```json
{
  "data": [
    {
      "id": "prod-001",
      "name": "Dell Latitude 5450 Laptop",
      "sku": "PRD-DELL-5450",
      "categoryName": "Computing",
      "costPrice": 45000.00,
      "sellingPrice": 55000.00,
      "unitOfMeasure": "EACH",
      "safetyStockQty": 10,
      "reorderPoint": 20,
      "abcCategory": "A"
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 10
}
```

**Tables Updated**: None (read-only search)

**Audit Entries Created**: No (search not audited)

**Notifications Created**: None

**Outbox Messages Created**: None

---

### 3.21 BARCODE SCAN

**API Endpoint**: `POST /api/v1/Barcodes/scan`

**Request Payload**:
```json
{
  "barcodeValue": "1234567890123",
  "action": "InventoryCount"
}
```

**Response Payload**:
```json
{
  "message": "Scan processed successfully.",
  "productId": "prod-001",
  "productName": "Dell Latitude 5450 Laptop",
  "productSKU": "PRD-DELL-5450",
  "totalOnHand": 98,
  "totalReserved": 0,
  "locations": [
    {
      "warehouseId": "wh-001",
      "warehouseName": "Madurai Main Warehouse",
      "zoneId": "z-001",
      "zoneName": "Electronics Zone",
      "binId": "b-001",
      "binCode": "Z-ELEC-B01",
      "quantityOnHand": 0
    },
    {
      "warehouseId": "wh-002",
      "warehouseName": "Chennai Distribution Hub",
      "zoneId": "z-005",
      "zoneName": "General Storage",
      "binId": "b-009",
      "binCode": "Z-GEN-B05",
      "quantityOnHand": 98
    }
  ],
  "scanLogConfirmation": "Scan logged: 2026-01-08 16:30:00 for Action: InventoryCount"
}
```

**Tables Updated**: `BarcodeScanLogs`

**Stock Level Updates**: None (read-only scan)

**Stock Movement Records Created**: None

**Audit Entries Created**: No (scan not audited as a direct CRUD)

**Notifications Created**: None

**Outbox Messages Created**: None

---

### 3.22 NOTIFICATION TRIGGERED

**API Endpoint**: `POST /api/v1/PurchaseOrders/{po-001}/approve`

**Notification Created** (when PO approved):
```json
{
  "id": "notif-001",
  "userId": "supplier-contact-id",
  "channel": "InApp",
  "type": "POApproved",
  "title": "Purchase Order Approved",
  "message": "Purchase Order PO-2026-001 has been approved. Please proceed with dispatch.",
  "entityType": "PurchaseOrder",
  "entityId": "po-001",
  "isRead": false,
  "createdAt": "2026-01-07T16:00:00Z"
}
```

**Outbox Message Created**:
```json
{
  "id": "outbox-001",
  "eventType": "SendNotification",
  "payload": {
    "userId": "supplier-contact-id",
    "channel": "InApp",
    "eventType": "POApproved",
    "title": "Purchase Order Approved",
    "message": "Purchase Order PO-2026-001 has been approved.",
    "entityType": "PurchaseOrder",
    "entityId": "po-001",
    "notificationId": "notif-001"
  },
  "status": "Pending",
  "createdAt": "2026-01-07T16:00:00Z"
}
```

**NotificationLog Created** (after processing):
```json
{
  "id": "notiflog-001",
  "userId": "supplier-contact-id",
  "channel": "InApp",
  "eventType": "POApproved",
  "recipient": "Raj Kumar",
  "status": "Sent",
  "sentAt": "2026-01-07T16:00:05Z",
  "createdAt": "2026-01-07T16:00:05Z"
}
```

---

### 3.23 AUDIT LOGS CREATED

**Audit Log Entry (Example - Product Created)**:
```json
{
  "id": "audit-001",
  "entityType": "Product",
  "entityId": "prod-001",
  "action": "Create",
  "oldValues": null,
  "newValues": {
    "Id": "prod-001",
    "Name": "Dell Latitude 5450 Laptop",
    "SKU": "PRD-DELL-5450",
    "CategoryId": "cat-002",
    "CostPrice": 45000.00,
    "SellingPrice": 55000.00,
    "IsActive": true,
    "CreatedAt": "2026-01-01T00:00:00Z"
  },
  "userId": "admin-user-id",
  "IpAddress": "192.168.1.100",
  "createdAt": "2026-01-01T00:00:01Z"
}
```

**Tables Updated**: `AuditLogs`

**Outbox Messages Created**: None (audit logs are synchronous)

---

## PHASE 4 — TRANSFER VARIANCE SCENARIO

### 4.1 SCENARIO SETUP

**Warehouse A (wh-001)** has 100 units of Product X (prod-001) in Bin B01.

**Transfer Request**: Transfer 100 units to Warehouse B (wh-002).

---

### 4.2 STEP-BY-STEP EXECUTION

#### Step 1: Initial State (Before Transfer)

**Warehouse A - prod-001, b-001**:
| Field | Value |
|-------|-------|
| QuantityOnHand | 100 |
| QuantityReserved | 0 |
| QuantityOnOrder | 0 |
| QuantityInTransit | 0 |

**Warehouse B - prod-001, b-009**:
| Field | Value |
|-------|-------|
| QuantityOnHand | 0 |
| QuantityReserved | 0 |
| QuantityOnOrder | 0 |
| QuantityInTransit | 0 |

---

#### Step 2: Transfer Requested

**API**: `POST /api/v1/Transfers`

**Stock Level Updates**:
- **Warehouse A**: Reserved = 100 (QOH=100, Reserved=100, OOH=0, IT=0)
- **Warehouse B**: InTransit = 100 (QOH=0, Reserved=0, OOH=100, IT=100)

**Transfer Items**:
| TransferId | ProductId | QuantityRequested | QuantityDispatched | QuantityReceived |
|------------|-----------|-------------------|-------------------|------------------|
| trf-001 | prod-001 | 100 | 0 | 0 |

---

#### Step 3: Transfer Approved

**API**: `PUT /api/v1/Transfers/{trf-001}/approve`

**No stock level changes** - stock remains reserved.

---

#### Step 4: Transfer Dispatched

**API**: `PUT /api/v1/Transfers/{trf-001}/dispatch`

**Warehouse A (wh-001)** - prod-001, b-001:
| Field | Before | After | Calculation |
|-------|--------|-------|-------------|
| QuantityOnHand | 100 | 0 | 100 - 100 (dispatched) |
| QuantityReserved | 100 | 0 | 100 - 100 (released) |

**Warehouse B (wh-002)** - prod-001, b-009:
| Field | Before | After | Calculation |
|-------|--------|-------|-------------|
| QuantityInTransit | 0 | 100 | 0 + 100 (dispatched) |

**Stock Movements Created**:
```
MovementType: TransferOut, ProductId: prod-001, WarehouseId: wh-001, Quantity: 100
MovementType: TransferOut, ProductId: prod-001, WarehouseId: wh-002, Quantity: 100
```

---

#### Step 5: Transfer Received (With Variance - Only 80 Received)

**API**: `PUT /api/v1/Transfers/{trf-001}/receive`

**Request Payload**:
```json
{
  "items": [
    {
      "transferItemId": "tri-001",
      "quantityReceived": 80,
      "overrideReason": null
    }
  ]
}
```

**Warehouse A (wh-001)** - prod-001, b-001:
| Field | Before | After | Calculation |
|-------|--------|-------|-------------|
| QuantityOnHand | 0 | 0 | No change (already dispatched) |
| QuantityReserved | 0 | 0 | No change |

**Warehouse B (wh-002)** - prod-001, b-009:
| Field | Before | After | Calculation |
|-------|--------|-------|-------------|
| QuantityInTransit | 100 | 0 | 100 - 100 (dispatched) |
| QuantityOnHand | 0 | 80 | 0 + 80 (received) |

**Stock Movements Created**:
```
MovementType: TransferIn, ProductId: prod-001, WarehouseId: wh-002, Quantity: 80
MovementType: WriteOff, ProductId: prod-001, WarehouseId: wh-002, Quantity: 20
```

**Transfer Status**: `ReceivedWithVariance`

**TransferItem Updates**:
| TransferId | ProductId | QuantityRequested | QuantityDispatched | QuantityReceived |
|------------|-----------|-------------------|-------------------|------------------|
| trf-001 | prod-001 | 100 | 100 | 80 |

**Variance Calculation**:
```
QuantityDispatched: 100
QuantityReceived: 80
Variance: 20 (Lost in Transit)
```

---

#### Step 6: Final State (After Variance Handling)

**Warehouse A - prod-001, b-001**:
| Field | Value | Notes |
|-------|-------|-------|
| QuantityOnHand | 0 | All units dispatched |
| QuantityReserved | 0 | Reservation released |
| QuantityOnOrder | 0 | - |
| QuantityInTransit | 0 | - |

**Warehouse B - prod-001, b-009**:
| Field | Value | Notes |
|-------|-------|-------|
| QuantityOnHand | 80 | Units actually received |
| QuantityReserved | 0 | - |
| QuantityOnOrder | 0 | - |
| QuantityInTransit | 0 | Transit cleared |

**Stock Level State** (Warehouse B):
- **Total QOH**: 80 units
- **Total Reserved**: 0 units
- **Total Lost**: 20 units (recorded as WriteOff movement)

---

#### Step 7: Accounting Proof

**Warehouse A (Origin)**:
| Account | Change |
|---------|--------|
| Inventory Asset | -100 units (removed) |

**Warehouse B (Destination)**:
| Account | Change |
|---------|--------|
| Inventory Asset | +80 units (received) |
| Loss/Expense | -20 units (variance write-off) |

**Net Effect**: -20 units total (Lost in Transit)

---

### 4.3 ARITHMETIC PROOF

```
Initial Stock (Warehouse A):     100 units
Transfer Requested:              100 units (Reserved)
Transfer Dispatched:             100 units (OnHand -= 100)
Transfer Received:                80 units (OnHand += 80)
Variances:
  - Quantity Dispatched:         100 units
  - Quantity Received:            80 units
  - Lost in Transit:             20 units (WriteOff)
Final Stock (Warehouse B):        80 units
Total Product Loss:               20 units
```

**Balancing Equation**:
```
Initial Stock (A) = Final Stock (B) + Lost in Transit
100 = 80 + 20 ✓
```

---

## PHASE 5 — BARCODE FLOW

### 5.1 BARCODE SUBSYSTEM OVERVIEW

The barcode subsystem in SmartInventory consists of:

1. **Barcode Generation** (Code128, QRCode)
2. **Barcode Assignment** (Primary/Secondary)
3. **Barcode Scanning** (With action tracking)
4. **Location Mapping** (Show stock locations on scan)

---

### 5.2 PRODUCT CREATION

**API**: `POST /api/v1/Products`

```json
{
  "name": "iPhone 15 Pro Max",
  "sku": "PRD-IP-15PM",
  "categoryId": "cat-003",
  "costPrice": 125000.00,
  "sellingPrice": 145000.00,
  "unitOfMeasure": "EACH",
  "reorderPoint": 10,
  "reorderQuantity": 20,
  "safetyStockQty": 5,
  "length": 15.0,
  "width": 7.5,
  "height": 0.8,
  "weightKg": 0.22,
  "preferredBinType": "Standard"
}
```

**Response**: Product created with ID `prod-004`

---

### 5.3 BARCODE GENERATION

**API**: `POST /api/v1/Barcodes/generate`

**Request**:
```json
{
  "productId": "prod-004",
  "barcodeValue": "1234567890126",
  "type": "Code128",
  "isPrimary": true
}
```

**Response**:
```json
{
  "id": "bc-005",
  "productId": "prod-004",
  "productName": "iPhone 15 Pro Max",
  "productSKU": "PRD-IP-15PM",
  "barcodeValue": "1234567890126",
  "barcodeType": "Code128",
  "isPrimary": true,
  "createdAt": "2026-01-08T17:00:00Z"
}
```

**Tables Updated**:
- `Barcodes`: Insert new record
- If `isPrimary=true`, all existing primaries for this product set to `isPrimary=false`

**Audit Entry**: Created

---

### 5.4 SECONDARY BARCODE

**API**: `POST /api/v1/Barcodes/generate`

**Request**:
```json
{
  "productId": "prod-004",
  "barcodeValue": "QR-IP-15PM-20260108",
  "type": "QRCode",
  "isPrimary": false
}
```

**Response**:
```json
{
  "id": "bc-014",
  "productId": "prod-004",
  "productName": "iPhone 15 Pro Max",
  "productSKU": "PRD-IP-15PM",
  "barcodeValue": "QR-IP-15PM-20260108",
  "barcodeType": "QRCode",
  "isPrimary": false,
  "createdAt": "2026-01-08T17:05:00Z"
}
```

**Tables Updated**:
- `Barcodes`: Insert new record
- Primary barcode unchanged (`isPrimary=true`)

---

### 5.5 BARCODE SCAN

**API**: `POST /api/v1/Barcodes/scan`

**Request**:
```json
{
  "barcodeValue": "1234567890126",
  "action": "Putaway"
}
```

**Response**:
```json
{
  "message": "Scan processed successfully.",
  "productId": "prod-004",
  "productName": "iPhone 15 Pro Max",
  "productSKU": "PRD-IP-15PM",
  "totalOnHand": 0,
  "totalReserved": 0,
  "locations": [],
  "scanLogConfirmation": "Scan logged: 2026-01-08 17:10:00 for Action: Putaway"
}
```

**Tables Updated**:
- `BarcodeScanLogs`: Insert new record
- `Product` (read): Product details

**BarcodeScanLog Entry**:
```json
{
  "id": "bsl-004",
  "barcodeId": "bc-005",
  "scannedBy": "u-004",
  "warehouseId": "wh-002",
  "action": "Putaway",
  "scannedAt": "2026-01-08T17:10:00Z",
  "createdAt": "2026-01-08T17:10:00Z"
}
```

**Audit Entry**: No (scan is not a CRUD operation on auditable entity)

---

### 5.6 BARCODE IMAGE GENERATION

**API**: `GET /api/v1/Barcodes/{bc-005}/image`

**Response**: Binary BMP file (32-bit, 300x100 pixels)

**Barcode Generation Algorithm**:
1.ZXing library converts `barcodeValue` to pixel data
2. BMP file header created (14 bytes)
3. Info header created (40 bytes)
4. Raw pixel data appended
5. Total file size calculated and stored

---

## PHASE 6 — OUTBOX FLOW

### 6.1 OUTBOX PROCESSOR OVERVIEW

**Service**: `OutboxProcessorService` (BackgroundService)

**Execution Model**:
- **Polling**: Every 30 seconds
- **LISTEN/NOTIFY**: PostgreSQL NOTIFY on `outbox_ready`

---

### 6.2 EVENT FLOW

#### Step 1: Stock Level Change Creates Outbox Record

**Trigger**: `AppDbContext.SaveChanges()` → `HandleStockLevelOutbox()`

```csharp
// In AppDbContext.SaveChangesAsync
var payload = new { ProductId = sl.ProductId, WarehouseId = sl.WarehouseId, QuantityOnHand = sl.QuantityOnHand };
outboxMessages.Add(new OutboxMessage
{
    Id = Guid.NewGuid(),
    EventType = "StockLevelChanged",
    Payload = JsonSerializer.Serialize(payload),
    Status = "Pending",
    CreatedAt = DateTime.UtcNow
});
```

**OutboxRecord Created**:
```json
{
  "id": "outbox-002",
  "eventType": "StockLevelChanged",
  "payload": "{\"ProductId\":\"prod-001\",\"WarehouseId\":\"wh-001\",\"QuantityOnHand\":100}",
  "status": "Pending",
  "retryCount": 0,
  "createdAt": "2026-01-08T15:30:00Z"
}
```

**Tables Updated**: `OutboxMessages`

---

#### Step 2: PostgreSQL LISTEN/NOTIFY

**Trigger**: `NOTIFY outbox_ready;` after `SaveChangesAsync`

**OutboxProcessorService.ListenForNotificationsAsync**:
```csharp
connection.Notification += async (o, e) =>
{
    if (e.Channel == "outbox_ready")
    {
        await ProcessOutboxAsync(stoppingToken);
    }
};
```

---

#### Step 3: Outbox Processing

**Query**:
```sql
SELECT * FROM outbox_messages 
WHERE "Status" IN ('Pending', 'Failed') 
   OR ("Status" = 'Processing' AND "ProcessedAt" IS NULL AND "CreatedAt" < NOW() - INTERVAL '5 minutes')
ORDER BY "CreatedAt"
LIMIT 100
FOR UPDATE SKIP LOCKED
```

---

#### Step 4: Processing Logic

**For StockLevelChanged Event**:
```csharp
if (msg.EventType == "StockLevelChanged")
{
    if (_redis != null)
    {
        var subscriber = _redis.GetSubscriber();
        await subscriber.PublishAsync(RedisChannel.Literal("realtime_stock_updates"), msg.Payload);
    }
    processed = true;
}
```

**Redis Publish**:
```
Channel: realtime_stock_updates
Message: {"ProductId":"prod-001","WarehouseId":"wh-001","QuantityOnHand":100}
```

**OutboxUpdate**:
```json
{
  "status": "Processed",
  "processedAt": "2026-01-08T15:30:05Z",
  "errorMessage": null
}
```

---

#### Step 5: Notification Event Processing

**For SendNotification Event**:
```csharp
else if (msg.EventType == "SendNotification")
{
    var payload = JsonSerializer.Deserialize<OutboxNotificationPayload>(msg.Payload);
    await ProcessNotificationMessageAsync(dbContext, realtimeService, payload, emailService);
}
```

**ProcessNotificationMessageAsync**:
1. Fetch user by `payload.UserId`
2. Send email if `channel == Email`
3. Send SMS if `channel == SMS`
4. Send in-app if `channel == InApp` (SignalR)
5. Create `NotificationLog` entry

**NotificationLog Entry**:
```json
{
  "id": "notiflog-002",
  "userId": "supplier-contact-id",
  "channel": "Email",
  "eventType": "POApproved",
  "recipient": "raj@abctech.com",
  "status": "Sent",
  "sentAt": "2026-01-08T15:30:05Z",
  "createdAt": "2026-01-08T15:30:05Z"
}
```

---

### 6.3 RETRY MECHANISM

**Retry Logic**:
```csharp
if (msg.RetryCount >= 3)
{
    msg.Status = "DeadLetter";
    continue;
}

// On exception:
msg.RetryCount++;
msg.Status = msg.RetryCount >= 3 ? "DeadLetter" : "Failed";
msg.ErrorMessage = ex.Message;
```

**Status Transitions**:
```
Pending → Processing → Processed
Pending → Processing → Failed (retryCount=1)
Failed → Processing → Processed
Failed → Processing → Failed (retryCount=2)
Failed → Processing → DeadLetter (retryCount=3)
```

**DeadLetter Handling**: Manual review required via database query

---

## PHASE 7 — AUDIT FLOW

### 7.1 AUDIT TRAIL MECHANISM

**Trigger**: `AppDbContext.SaveChangesAsync()` → `OnBeforeSaveChanges()`

**Audit Entry Collection**:
```csharp
foreach (var entry in ChangeTracker.Entries())
{
    if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
        continue;

    var auditEntry = new AuditEntry(entry)
    {
        TableName = entry.Entity.GetType().Name,
        UserId = currentUserId,
        IpAddress = _currentUserService?.IpAddress
    };
    auditEntries.Add(auditEntry);

    foreach (var property in entry.Properties)
    {
        string propertyName = property.Metadata.Name;
        if (property.Metadata.IsPrimaryKey()) continue;

        switch (entry.State)
        {
            case EntityState.Added:
                auditEntry.Action = "Create";
                auditEntry.NewValues[propertyName] = property.CurrentValue;
                break;
            case EntityState.Deleted:
                auditEntry.Action = "Delete";
                auditEntry.OldValues[propertyName] = property.OriginalValue;
                break;
            case EntityState.Modified:
                if (property.IsModified)
                {
                    auditEntry.Action = "Update";
                    auditEntry.OldValues[propertyName] = property.OriginalValue;
                    auditEntry.NewValues[propertyName] = property.CurrentValue;
                }
                break;
        }
    }
}
```

---

### 7.2 AUDIT LOG CREATION

**Save Audit Logs**:
```csharp
foreach (var auditEntry in auditEntries)
{
    AuditLogs.Add(auditEntry.ToAuditLog());
}
```

**AuditLog Record**:
```json
{
  "id": "audit-002",
  "entityType": "StockLevel",
  "entityId": "sl-001",
  "action": "Update",
  "oldValues": "{\"QuantityOnHand\":100}",
  "newValues": "{\"QuantityOnHand\":105}",
  "userId": "u-003",
  "ipAddress": "192.168.1.100",
  "createdAt": "2026-01-08T15:30:00Z"
}
```

**Tables Updated**:
- `AuditLogs`: Insert new record
- Delayed commit (separate transaction after main save)

---

### 7.3 COMPLETE AUDIT TRAIL EXAMPLE

**Scenario**: User `u-003` creates stock adjustment that increases stock by 5 units.

**Sequence of Events**:

1. **StockAdjustment Created**:
   ```json
   {
     "action": "Create",
     "entityType": "StockAdjustment",
     "entityId": "adj-002",
     "oldValues": null,
     "newValues": {
       "Id": "adj-002",
       "QuantityBefore": 100,
       "QuantityAfter": 105,
       "QuantityChange": 5
     }
   }
   ```

2. **StockLevel Updated**:
   ```json
   {
     "action": "Update",
     "entityType": "StockLevel",
     "entityId": "sl-002",
     "oldValues": "{\"QuantityOnHand\":100}",
     "newValues": "{\"QuantityOnHand\":105}"
   }
   ```

3. **StockMovement Created**:
   ```json
   {
     "action": "Create",
     "entityType": "StockMovement",
     "entityId": "sm-013",
     "oldValues": null,
     "newValues": {
       "MovementType": "Adjustment",
       "Quantity": 5
     }
   }
   ```

**All audit entries captured in chronological order with user context**.

---

### 7.4 OVERRIDE AUDIT LOG (Capacity Override)

**Trigger**: When user overrides capacity warning in GRN or Adjustment

**Audit Entry**:
```csharp
var auditLog = new OverrideAuditLog
{
    Id = Guid.NewGuid(),
    UserId = _currentUserService.UserId,
    Timestamp = DateTime.UtcNow,
    RuleBroken = "ZoneMismatch",
    OverrideReason = "Manual Override - Product needs immediate putaway",
    TargetBinId = binId,
    ProductId = productId
};
await _uow.Repository<OverrideAuditLog>().AddAsync(auditLog);
```

**OverrideAuditLog Entry**:
```json
{
  "id": "oal-001",
  "userId": "u-001",
  "timestamp": "2026-01-08T15:45:00Z",
  "ruleBroken": "ZoneMismatch",
  "overrideReason": "Manual Override - Product needs immediate putaway",
  "targetBinId": "b-001",
  "productId": "prod-001",
  "createdAt": "2026-01-08T15:45:00Z"
}
```

---

## PHASE 8 — TEST PLAN

### 8.1 AUTHENTICATION TESTS

| Test ID | Test Case | Method | Endpoint | Request | Expected | Status |
|---------|-----------|--------|----------|---------|----------|--------|
| AUTH-001 | Valid login | POST | /api/v1/Auth/signin | LoginDto | 200 + JWT tokens | [ ] |
| AUTH-002 | Invalid password | POST | /api/v1/Auth/signin | Wrong password | 401 | [ ] |
| AUTH-003 | Non-existent user | POST | /api/v1/Auth/signin | Invalid email | 401 | [ ] |
| AUTH-004 | Token refresh | POST | /api/v1/Auth/refresh | Valid refresh token | 200 + new tokens | [ ] |
| AUTH-005 | Expired token refresh | POST | /api/v1/Auth/refresh | Expired token | 401 | [ ] |
| AUTH-006 | Revoke token | POST | /api/v1/Auth/revoke | Valid refresh token | 204 | [ ] |
| AUTH-007 | Change password | PUT | /api/v1/Auth/change-password | Valid password | 204 | [ ] |
| AUTH-008 | Rate limiting (5 req/60s) | POST x6 | /api/v1/Auth/signin | Multiple logins | 429 on 6th request | [ ] |

---

### 8.2 WAREHOUSE TESTS

| Test ID | Test Case | Method | Endpoint | Expected |
|---------|-----------|--------|----------|----------|
| WH-001 | Create warehouse | POST /api/v1/Warehouses | 201 + warehouse | [ ] |
| WH-002 | List warehouses | GET /api/v1/Warehouses | 200 + paged results | [ ] |
| WH-003 | Get warehouse by ID | GET /api/v1/Warehouses/{id} | 200 | [ ] |
| WH-004 | Update warehouse | PUT /api/v1/Warehouses/{id} | 200 | [ ] |
| WH-005 | Soft delete warehouse | DELETE /api/v1/Warehouses/{id} | 204 | [ ] |
| WH-006 | Create zone | POST /api/v1/Warehouses/{id}/zones | 200 | [ ] |
| WH-007 | Create bin | POST /api/v1/Warehouses/zones/{id}/bins | 200 | [ ] |
| WH-008 | Get putaway suggestion | GET /api/v1/Warehouses/{id}/putaway-suggestion?productId=x | 200 | [ ] |
| WH-009 | Assign user access | POST /api/v1/Warehouses/{id}/users | 200 | [ ] |
| WH-010 | Revoke user access | DELETE /api/v1/Warehouses/access/{id} | 204 | [ ] |
| WH-011 | Duplicate code validation | POST /api/v1/Warehouses | Duplicate code | 400 | [ ] |
| WH-012 | Zone capacity enforcement | POST bin with isCapacityEnforced=true | Valid + invalid capacity | 200 + validation | [ ] |

---

### 8.3 STOCK ADJUSTMENT TESTS

| Test ID | Test Case | Method | Endpoint | Expected |
|---------|-----------|--------|----------|----------|
| ADJ-001 | Create adjustment (auto-approve) | POST /api/v1/StockAdjustments | Variance < 5% | 200 + approved | [ ] |
| ADJ-002 | Create adjustment (pending approval) | POST /api/v1/StockAdjustments | Variance > 5% | 202 + pending | [ ] |
| ADJ-003 | Approve adjustment | PUT /api/v1/StockAdjustments/{id}/approve | Approved by Manager | 200 | [ ] |
| ADJ-004 | Reject adjustment | PUT /api/v1/StockAdjustments/{id}/approve | Reject with notes | 200 | [ ] |
| ADJ-005 | Cancel adjustment | POST /api/v1/StockAdjustments/{id}/cancel | Reversal | 200 | [ ] |
| ADJ-006 | Negative quantity validation | POST /api/v1/StockAdjustments | quantityAfter < 0 | 400 | [ ] |
| ADJ-007 | Own approval validation | PUT /api/v1/StockAdjustments/{id}/approve | Performer = Approver | 400 | [ ] |
| ADJ-008 | Permission validation | PUT /api/v1/StockAdjustments/{id}/approve | Non-manager user | 403 | [ ] |

---

### 8.4 TRANSFER TESTS

| Test ID | Test Case | Method | Endpoint | Expected |
|---------|-----------|--------|----------|----------|
| TRF-001 | Create transfer | POST /api/v1/Transfers | Valid request | 201 | [ ] |
| TRF-002 | Approve transfer | PUT /api/v1/Transfers/{id}/approve | Approve | 200 | [ ] |
| TRF-003 | Reject transfer | PUT /api/v1/Transfers/{id}/approve | Reject | 200 | [ ] |
| TRF-004 | Dispatch transfer | PUT /api/v1/Transfers/{id}/dispatch | InTransit status | 200 | [ ] |
| TRF-005 | Receive transfer | PUT /api/v1/Transfers/{id}/receive | Received status | 200 | [ ] |
| TRF-006 | Transfer with variance | PUT /api/v1/Transfers/{id}/receive | 80 received of 100 | 200 + ReceivedWithVariance | [ ] |
| TRF-007 | Same warehouse validation | POST /api/v1/Transfers | From = To | 400 | [ ] |
| TRF-008 | Insufficient stock | POST /api/v1/Transfers | Quantity > available | 400 | [ ] |
| TRF-009 | Transfer bin-to-bin | POST /api/v1/Transfers/bin-to-bin | Valid request | 200 | [ ] |

---

### 8.5 PURCHASE ORDER TESTS

| Test ID | Test Case | Method | Endpoint | Expected |
|---------|-----------|--------|----------|----------|
| PO-001 | Create PO | POST /api/v1/PurchaseOrders | Valid request | 201 | [ ] |
| PO-002 | Submit PO | PUT /api/v1/PurchaseOrders/{id}/submit | Submitted status | 200 | [ ] |
| PO-003 | Approve PO | PUT /api/v1/PurchaseOrders/{id}/approve | Approved status | 200 | [ ] |
| PO-004 | Cancel PO | PUT /api/v1/PurchaseOrders/{id}/cancel | Cancelled status | 200 | [ ] |
| PO-005 | Receive goods | POST /api/v1/PurchaseOrders/{id}/grn | GRN created | 200 | [ ] |
| PO-006 | GRN with rejection | POST /api/v1/PurchaseOrders/{id}/grn | Partial rejection | 200 + PartiallyReceived | [ ] |
| PO-007 | Cancel GRN | POST /api/v1/PurchaseOrders/receipts/{id}/cancel | GRN cancelled | 200 | [ ] |
| PO-008 | PO price validation | POST /api/v1/PurchaseOrders | Price > catalogue | 400 | [ ] |
| PO-009 | PO quantity validation | POST /api/v1/PurchaseOrders | Quantity < MOQ | 400 | [ ] |
| PO-010 | GRN attachment validation | POST /api/v1/PurchaseOrders/{id}/grn | No delivery Challan | 400 | [ ] |

---

### 8.6 BARCODE TESTS

| Test ID | Test Case | Method | Endpoint | Expected |
|---------|-----------|--------|----------|----------|
| BC-001 | Generate barcode | POST /api/v1/Barcodes/generate | Valid request | 200 + barcode | [ ] |
| BC-002 | Primary barcode demotion | POST /api/v1/Barcodes/generate | isPrimary=true on existing | 200 + old primary demoted | [ ] |
| BC-003 | Duplicate barcode | POST /api/v1/Barcodes/generate | Duplicate value | 400 | [ ] |
| BC-004 | Scan barcode | POST /api/v1/Barcodes/scan | Valid scan | 200 + scan result | [ ] |
| BC-005 | Get product barcodes | GET /api/v1/Barcodes/product/{id} | 200 + list | [ ] |
| BC-006 | Get barcode image | GET /api/v1/Barcodes/{id}/image | 200 + BMP file | [ ] |
| BC-007 | Batch generate | POST /api/v1/Barcodes/batch-generate | 500 items | 200 | [ ] |
| BC-008 | Barcode scan log | POST /api/v1/Barcodes/scan | Action=InventoryCount | 200 + scan log created | [ ] |

---

### 8.7 NOTIFICATION TESTS

| Test ID | Test Case | Method | Endpoint | Expected |
|---------|-----------|--------|----------|----------|
| NOTIF-001 | Get user inbox | GET /api/v1/Notifications | 200 + paged | [ ] |
| NOTIF-002 | Get unread count | GET /api/v1/Notifications/unread-count | 200 + count | [ ] |
| NOTIF-003 | Mark read | PUT /api/v1/Notifications/{id}/read | 204 | [ ] |
| NOTIF-004 | Mark all read | PUT /api/v1/Notifications/read-all | 204 | [ ] |

---

### 8.8 SUPPLIER TESTS

| Test ID | Test Case | Method | Endpoint | Expected |
|---------|-----------|--------|----------|----------|
| SUP-001 | Create supplier | POST /api/v1/Suppliers | Valid request | 201 | [ ] |
| SUP-002 | Review supplier (approve) | POST /api/v1/Suppliers/{id}/review | Approve | 200 | [ ] |
| SUP-003 | Review supplier (reject) | POST /api/v1/Suppliers/{id}/review | Reject | 200 | [ ] |
| SUP-004 | Add supplier product | POST /api/v1/Suppliers/{id}/products | Valid request | 201 | [ ] |
| SUP-005 | Update supplier product | PUT /api/v1/Suppliers/products/{id} | 200 | [ ] |
| SUP-006 | Suspend supplier | POST /api/v1/Suppliers/{id}/suspend | 204 | [ ] |
| SUP-007 | Activate supplier | POST /api/v1/Suppliers/{id}/activate | 204 | [ ] |
| SUP-008 | Supplier self-registration | POST /api/v1/supplier/auth/register | 200 | [ ] |
| SUP-009 | Email verification | POST /api/v1/supplier/auth/verify-email | 200 | [ ] |
| SUP-010 | Supplier login | POST /api/v1/supplier/auth/login | 200 + JWT | [ ] |

---

### 8.9 CONCURRENCY TESTS

| Test ID | Test Case | Method | Endpoint | Expected |
|---------|-----------|--------|----------|----------|
| CON-001 | Concurrent stock update | PUT x2 | StockLevel same second | 409 (stale data) | [ ] |
| CON-002 | Concurrent PO update | PUT x2 | PO update same second | 409 | [ ] |
| CON-003 | Concurrent transfer update | PUT x2 | Transfer update same second | 409 | [ ] |

---

### 8.10 SECURITY TESTS

| Test ID | Test Case | Method | Endpoint | Expected |
|---------|-----------|--------|----------|----------|
| SEC-001 | Unauthenticated access | GET /api/v1/Products | No token | 401 | [ ] |
| SEC-002 | Wrong permission | GET /api/v1/Suppliers | Staff role | 403 | [ ] |
| SEC-003 | IDOR PO access | GET /api/v1/PurchaseOrders/{other-po} | Warehouse mismatch | 403 | [ ] |
| SEC-004 | SQL injection attempt | POST /api/v1/Products/search | Malicious filter | 400 | [ ] |
| SEC-005 | XSS attempt | POST /api/v1/Warehouses | HTML in name | 400 | [ ] |
| SEC-006 | Rate limit reports | GET x15 | /api/v1/Reports | 429 | [ ] |
| SEC-007 | Supplier isolated data | GET /api/v1/supplier/purchase-orders | Cross-supplier PO | 404 | [ ] |

---

### 8.11 BOUNDARY TESTS

| Test ID | Test Case | Method | Endpoint | Expected |
|---------|-----------|--------|----------|----------|
| BND-001 | Zero quantity adjustment | POST /api/v1/StockAdjustments | quantityAfter=0 | 400 | [ ] |
| BND-002 | Negative quantity | POST /api/v1/StockAdjustments | quantityAfter=-5 | 400 | [ ] |
| BND-003 | Large quantity | POST /api/v1/StockAdjustments | quantityAfter=999999999 | 400 | [ ] |
| BND-004 | Empty transfer items | POST /api/v1/Transfers | items=[] | 400 | [ ] |
| BND-005 | Max batch generate | POST /api/v1/Barcodes/batch-generate | 501 items | 400 | [ ] |
| BND-006 | File size limit | POST /api/v1/Files/upload | 11MB file | 400 | [ ] |
| BND-007 | Invalid email format | POST /api/v1/Suppliers | invalid@ | 400 | [ ] |
| BND-008 | Missing required field | POST /api/v1/Products | no SKU | 400 | [ ] |

---

This is a comprehensive test plan for the SmartInventory WMS system.

---

## PHASE 9 — TECHNICAL DEBT & LIMITATIONS

### 9.1 RESERVED CAPACITY (TRANSFERS RACE CONDITION)
**Status**: Deferred to Phase 2  
**Risk Level**: Medium  

**Description**: 
Currently, the Capacity Engine operates strictly on materialized physical stock (`QuantityOnHand`). When a Transfer is dispatched, the capacity is correctly released from the source bin. However, the system does **not** allocate or "reserve" capacity at the destination bin while the goods are in transit. 

**Operational Impact**:
Because the destination bin's available capacity remains open during the transfer period, a newly arrived Goods Receipt (GRN) could be put away into that bin, consuming its remaining capacity. When the Transfer finally arrives and is marked as "Received", the receipt operation may fail with a `CapacityExceeded` error if the bin is now full.

**Workaround**:
Warehouse managers should ensure destination bins for large transfers are kept clear or marked as unavailable until the transfer arrives, or instruct staff to receive the transfer into a different bin if the original destination bin has become full.

**Future Solution (Phase 2)**:
Introduce `ReservedVolumeCm3` and `ReservedWeightKg` to `BinLocation`. Transfer Dispatch will add to the reserved capacity, and Transfer Receive will convert reserved capacity into utilized capacity.

---

**END OF OPERATIONAL HANDBOOK**

