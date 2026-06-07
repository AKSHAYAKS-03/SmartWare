# Barcode Management Enterprise Refactoring

This plan outlines the refactoring of the Barcode Management subsystem to eliminate Clean Architecture violations and implement full operational context for warehouse barcode scans.

## Goal Description
1. **Architectural Purity**: Remove all direct `IUnitOfWork` references from `BarcodesController.cs`. Business logic, database mutations, and transactions will be securely encapsulated inside `BarcodeService.cs`.
2. **Contextual Scanning**: Enhance the `POST /api/v1/barcodes/scan` endpoint. Instead of returning just the product name and SKU, it will return a comprehensive operational payload (`ScanResultDto`) detailing physical stock locations, available quantities, and reserved quantities.

## Proposed Changes

---

### Core Layer (`SmartInventory.Core`)

#### [MODIFY] [IBarcodeService.cs](file:///Users/akshaya/SmartWare/SmartInventory/SmartInventory.Core/Interfaces/IBarcodeService.cs)
- Add asynchronous methods for domain operations:
  - `Task<BarcodeResponseDto> GenerateBarcodeRecordAsync(BarcodeGenerateRequestDto request);`
  - `Task<ScanResultDto> ProcessScanAsync(BarcodeScanDto dto, Guid currentUserId, Guid? currentWarehouseId);`
  - `Task<IEnumerable<BarcodeResponseDto>> GetProductBarcodesAsync(Guid productId);`

#### [NEW] `SmartInventory.Core/DTOs/BarcodeDtos.cs`
- Centralize Barcode DTOs (moving `BarcodeGenerateRequest` out of the controller).
- Create `ScanResultDto` containing:
  - Product Name, SKU, Category
  - `TotalOnHand`, `TotalReserved`, `TotalAvailable`
  - `List<StockLocationDto>` (mapping Warehouse -> Zone -> Aisle -> Rack -> Bin -> Quantity)
  - Scan log confirmation text.

---

### Service Layer (`SmartInventory.Service`)

#### [MODIFY] [BarcodeService.cs](file:///Users/akshaya/SmartWare/SmartInventory/SmartInventory.Service/Services/BarcodeService.cs)
- Inject `IUnitOfWork`.
- **`GenerateBarcodeRecordAsync`**: Migrate the uniqueness check and `_uow` save logic from the controller into this method.
- **`ProcessScanAsync`**: 
  - Look up the barcode.
  - Query all `StockLevel` records for the mapped `ProductId`, including `BinLocation` and `Zone` relationships.
  - Sum `QuantityOnHand` and `QuantityReserved`.
  - Log the scan to `BarcodeScanLog` and `_uow.CommitAsync()`.
  - Map and return the rich `ScanResultDto`.
- **`GetProductBarcodesAsync`**: Migrate the query logic from the controller into this method.

---

### API Layer (`SmartInventory.API`)

#### [MODIFY] [BarcodesController.cs](file:///Users/akshaya/SmartWare/SmartInventory/SmartInventory.API/Controllers/BarcodesController.cs)
- Completely remove `IUnitOfWork` dependency.
- Inject `ICurrentUserService`.
- **`GenerateBarcode`**: Delegate directly to `_barcodeService.GenerateBarcodeRecordAsync(request)`.
- **`ScanBarcode`**: Delegate directly to `_barcodeService.ProcessScanAsync(dto, _currentUser.UserId, _currentUser.WarehouseId)`.
- **`GetProductBarcodes`**: Delegate to `_barcodeService.GetProductBarcodesAsync(productId)`.

---

## Verification Plan

### Automated Build
- Run `dotnet build SmartInventory.API` to ensure interface contracts and clean architecture dependencies are valid.

### Manual Verification
- The `ScanBarcode` endpoint will be tested to confirm it returns the full array of stock locations (Warehouse -> Zone -> Bin) and available quantities instead of the previous shallow response.
- Attempting to generate a duplicate barcode will hit the `BusinessRuleException` inside the Service layer, returning a clean 400 Bad Request.

> [!IMPORTANT]
> Does the `ScanResultDto` need to explicitly look up pending `WarehouseTransfer` items to list the specific Transfer IDs holding the reserved quantity, or is the total `QuantityReserved` field sufficient for this pass?


Here is a brief overview of what we are building for the Barcode Scan and the overall Barcode Management flow.

### 1. What the Barcode Scan endpoint will do
When a warehouse worker scans a barcode using their physical scanner or phone camera, the system will now be **context-aware** using the `Action` field you suggested.

*   **Action = "Lookup" (What we are building now)**: The worker just scans an item on the floor. The API returns the Product Name, SKU, and a complete map of **every bin in every warehouse** where that item is stored, along with the total Available and Reserved quantities.
*   **Action = "GRN_Receive"**: The worker scans an item as it comes off a truck. The system automatically finds the open Purchase Order, verifies the item belongs to it, and logs the receipt.
*   **Action = "Transfer_Pick"**: The worker scans a barcode on a shelf to confirm they are taking the correct item for a dispatch transfer. It validates the pick and reserves the stock.
*   **Action = "Cycle_Count"**: The worker scans an item during an audit. The system opens a stock adjustment form for that specific bin and product.

*In all cases, the scan is securely logged into `BarcodeScanLog` with the user's ID, warehouse, and timestamp for auditing.*

### 2. The Complete Flow of Barcode Management
Here is how the end-to-end lifecycle works in your system:

1.  **Generation (Creation)**: When a new Product is created in the system, `BarcodeService` automatically generates a primary `Code128` barcode mapping to its SKU. It generates the pure physical image (BMP/PNG) so you can print it on a sticky label.
2.  **Labeling (Physical)**: Staff prints the labels and sticks them to the physical boxes or racks in the warehouse.
3.  **Scanning (Interaction)**: A worker scans the label. The scanner acts like a super-fast keyboard, typing the Barcode string into your frontend (Angular/React) application.
4.  **Processing (API Call)**: The frontend sends the string to our `POST /api/v1/barcodes/scan` endpoint.
5.  **Execution (Service Layer)**: 
    *   Our newly refactored `BarcodeService.cs` intercepts it.
    *   It checks the Database for uniqueness and validity.
    *   It pulls the rich `StockLevel` and `BinLocation` data.
    *   It executes the requested "Action" (like picking or receiving).
6.  **Audit Trail (Security)**: The system permanently records who scanned it, when, and where (`BarcodeScanLog`), completely decoupled from the controllers to ensure Clean Architecture.

Shall I go ahead and execute the Implementation Plan to migrate the database logic out of the controller and build out the rich "Lookup" scan response?