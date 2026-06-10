using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Exceptions;
using SmartInventory.Core.Interfaces;
using Mapster;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using System.Security.Claims;

namespace SmartInventory.Service.Services;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notificationService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;
    private readonly MediatR.IPublisher _publisher;
    private readonly IInventoryValuationService _valuationService;
    private readonly IAuthorizationService _authorizationService;

    public PurchaseOrderService(IUnitOfWork uow, INotificationService notificationService, ICurrentUserService currentUserService, ICacheService cacheService, MediatR.IPublisher publisher, IInventoryValuationService valuationService, IAuthorizationService authorizationService)
    {
        _uow = uow;
        _notificationService = notificationService;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
        _publisher = publisher;
        _valuationService = valuationService;
        _authorizationService = authorizationService;
    }

    public async Task<PurchaseOrderResponseDto> CreatePurchaseOrderAsync(PurchaseOrderCreateDto dto)
    {
        if (!string.IsNullOrEmpty(dto.IdempotencyKey))
        {
            var existingPoId = await _cacheService.GetAsync<Guid?>($"Idempotency_PO_{dto.IdempotencyKey}");
            if (existingPoId != null)
            {
                return await GetPurchaseOrderByIdAsync(existingPoId.Value);
            }
        }
        var supplier = await _uow.Repository<Supplier>().GetByIdAsync(dto.SupplierId);
        if (supplier == null)
            throw new NotFoundException("Supplier", dto.SupplierId);

        var warehouse = await _uow.Repository<Warehouse>().GetByIdAsync(dto.WarehouseId);
        if (warehouse == null)
            throw new NotFoundException("Warehouse", dto.WarehouseId);

        var currentUserId = _currentUserService.UserId;
        var user = await _uow.Repository<User>().GetByIdAsync(currentUserId);
        if (user == null)
            throw new NotFoundException("User", currentUserId);

        var poId = Guid.NewGuid();

        var items = new List<PurchaseOrderItem>();
        decimal totalAmount = 0;
        int maxLeadTimeDays = 0;

        foreach (var itemDto in dto.Items)
        {
            if (itemDto.QuantityOrdered <= 0)
                throw new BusinessRuleException($"Quantity ordered for Product ID {itemDto.ProductId} must be greater than zero.");

            var product = await _uow.Repository<Product>().GetByIdAsync(itemDto.ProductId);
            if (product == null)
                throw new NotFoundException("Product", itemDto.ProductId);

            var supplierProduct = await _uow.Repository<SupplierProduct>()
                .Query()
                .FirstOrDefaultAsync(sp => sp.SupplierId == dto.SupplierId && sp.ProductId == itemDto.ProductId);

            if (supplierProduct == null || !supplierProduct.IsActive)
                throw new BusinessRuleException($"Product {product.Name} is not actively offered by this supplier.");

            if (itemDto.UnitPrice > supplierProduct.UnitPrice)
                throw new BusinessRuleException($"Unit price for {product.Name} ({itemDto.UnitPrice}) exceeds the catalogue contract price ({supplierProduct.UnitPrice}).");

            // ── PO Price Tolerance Guard (Lower-Bound) ────────────────────────────
            // Deviation = ABS(CataloguePrice - POPrice) / CataloguePrice
            // If deviation > 20%, reject immediately. This prevents accidental data-entry
            // errors from corrupting WAC, inventory valuation, EOQ and financial reports.
            if (supplierProduct.UnitPrice > 0)
            {
                decimal deviation = Math.Abs(supplierProduct.UnitPrice - itemDto.UnitPrice) / supplierProduct.UnitPrice;
                if (deviation > 0.20m)
                {
                    decimal minimumAllowedPrice = Math.Round(supplierProduct.UnitPrice * 0.80m, 2);
                    throw new BusinessRuleException(
                        $"Unit price for {product.Name} ({itemDto.UnitPrice:N2}) deviates {deviation:P0} from the catalogue price ({supplierProduct.UnitPrice:N2}), " +
                        $"which exceeds the maximum allowed deviation of 20%. " +
                        $"Minimum allowed PO price is {minimumAllowedPrice:N2}. " +
                        $"Please verify the price or raise a contract amendment.");
                }
            }

            if (itemDto.QuantityOrdered < supplierProduct.MinOrderQuantity)
                throw new BusinessRuleException($"Order quantity for {product.Name} ({itemDto.QuantityOrdered}) is below the minimum order quantity ({supplierProduct.MinOrderQuantity}).");

            if (supplierProduct.LeadTimeDays > maxLeadTimeDays)
                maxLeadTimeDays = supplierProduct.LeadTimeDays;

            var item = new PurchaseOrderItem
            {
                Id = Guid.NewGuid(),
                PurchaseOrderId = poId,
                ProductId = itemDto.ProductId,
                QuantityOrdered = itemDto.QuantityOrdered,
                QuantityReceived = 0,
                UnitPrice = itemDto.UnitPrice,
                TotalPrice = itemDto.QuantityOrdered * itemDto.UnitPrice
            };

            items.Add(item);
            totalAmount += item.TotalPrice;
        }

        var earliestDeliveryDate = DateTime.UtcNow.AddDays(maxLeadTimeDays).Date;
        if (dto.ExpectedDelivery.HasValue && dto.ExpectedDelivery.Value.Date < earliestDeliveryDate)
            throw new BusinessRuleException($"Expected delivery date ({dto.ExpectedDelivery:yyyy-MM-dd}) violates the supplier's minimum lead time of {maxLeadTimeDays} days. Earliest possible delivery is {earliestDeliveryDate:yyyy-MM-dd}.");

        var po = new PurchaseOrder
        {
            Id = poId,
            SupplierId = dto.SupplierId,
            WarehouseId = dto.WarehouseId,
            CreatedBy = currentUserId,
            Status = PurchaseOrderStatus.Draft,
            TotalAmount = totalAmount,
            ExpectedDelivery = dto.ExpectedDelivery,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow,
            Items = items
        };

        await _uow.Repository<PurchaseOrder>().AddAsync(po);
        await _uow.CommitAsync();

        if (!string.IsNullOrEmpty(dto.IdempotencyKey))
        {
            await _cacheService.SetAsync($"Idempotency_PO_{dto.IdempotencyKey}", poId, TimeSpan.FromHours(24));
        }

        return await GetPurchaseOrderByIdAsync(poId);
    }

    public async Task<PurchaseOrderResponseDto> UpdatePurchaseOrderAsync(Guid poId, PurchaseOrderUpdateDto dto)
    {
        var po = await _uow.Repository<PurchaseOrder>()
            .Query()
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == poId);

        if (po == null)
            throw new NotFoundException("PurchaseOrder", poId);

        if (po.Status != PurchaseOrderStatus.Draft)
            throw new BusinessRuleException("Only draft purchase orders can be edited.");

        var supplier = await _uow.Repository<Supplier>().GetByIdAsync(dto.SupplierId);
        if (supplier == null)
            throw new NotFoundException("Supplier", dto.SupplierId);

        var warehouse = await _uow.Repository<Warehouse>().GetByIdAsync(dto.WarehouseId);
        if (warehouse == null)
            throw new NotFoundException("Warehouse", dto.WarehouseId);

        po.Items.Clear();

        decimal totalAmount = 0;
        int maxLeadTimeDays = 0;
        foreach (var itemDto in dto.Items)
        {
            if (itemDto.QuantityOrdered <= 0)
                throw new BusinessRuleException($"Quantity ordered for Product ID {itemDto.ProductId} must be greater than zero.");

            var product = await _uow.Repository<Product>().GetByIdAsync(itemDto.ProductId);
            if (product == null)
                throw new NotFoundException("Product", itemDto.ProductId);

            var supplierProduct = await _uow.Repository<SupplierProduct>()
                .Query()
                .FirstOrDefaultAsync(sp => sp.SupplierId == dto.SupplierId && sp.ProductId == itemDto.ProductId);

            if (supplierProduct == null || !supplierProduct.IsActive)
                throw new BusinessRuleException($"Product {product.Name} is not actively offered by this supplier.");

            if (itemDto.UnitPrice > supplierProduct.UnitPrice)
                throw new BusinessRuleException($"Unit price for {product.Name} ({itemDto.UnitPrice}) exceeds the catalogue contract price ({supplierProduct.UnitPrice}).");

            // ── PO Price Tolerance Guard (Lower-Bound) ────────────────────────────
            // Same 20% deviation rule applied on edits (only reachable in Draft status).
            if (supplierProduct.UnitPrice > 0)
            {
                decimal deviation = Math.Abs(supplierProduct.UnitPrice - itemDto.UnitPrice) / supplierProduct.UnitPrice;
                if (deviation > 0.20m)
                {
                    decimal minimumAllowedPrice = Math.Round(supplierProduct.UnitPrice * 0.80m, 2);
                    throw new BusinessRuleException(
                        $"Unit price for {product.Name} ({itemDto.UnitPrice:N2}) deviates {deviation:P0} from the catalogue price ({supplierProduct.UnitPrice:N2}), " +
                        $"which exceeds the maximum allowed deviation of 20%. " +
                        $"Minimum allowed PO price is {minimumAllowedPrice:N2}. " +
                        $"Please verify the price or raise a contract amendment.");
                }
            }

            if (itemDto.QuantityOrdered < supplierProduct.MinOrderQuantity)
                throw new BusinessRuleException($"Order quantity for {product.Name} ({itemDto.QuantityOrdered}) is below the minimum order quantity ({supplierProduct.MinOrderQuantity}).");

            if (supplierProduct.LeadTimeDays > maxLeadTimeDays)
                maxLeadTimeDays = supplierProduct.LeadTimeDays;

            var item = new PurchaseOrderItem
            {
                Id = Guid.NewGuid(),
                PurchaseOrderId = poId,
                ProductId = itemDto.ProductId,
                QuantityOrdered = itemDto.QuantityOrdered,
                QuantityReceived = 0,
                UnitPrice = itemDto.UnitPrice,
                TotalPrice = itemDto.QuantityOrdered * itemDto.UnitPrice
            };

            await _uow.Repository<PurchaseOrderItem>().AddAsync(item);
            totalAmount += item.TotalPrice;
        }

        var earliestDeliveryDate = DateTime.UtcNow.AddDays(maxLeadTimeDays).Date;
        if (dto.ExpectedDelivery.HasValue && dto.ExpectedDelivery.Value.Date < earliestDeliveryDate)
            throw new BusinessRuleException($"Expected delivery date ({dto.ExpectedDelivery:yyyy-MM-dd}) violates the supplier's minimum lead time of {maxLeadTimeDays} days. Earliest possible delivery is {earliestDeliveryDate:yyyy-MM-dd}.");

        po.SupplierId = dto.SupplierId;
        po.WarehouseId = dto.WarehouseId;
        po.ExpectedDelivery = dto.ExpectedDelivery;
        po.Notes = dto.Notes;
        po.TotalAmount = totalAmount;

        await _uow.CommitAsync();

        return await GetPurchaseOrderByIdAsync(poId);
    }

    public async Task<PurchaseOrderResponseDto> SubmitForApprovalAsync(Guid poId)
    {
        var po = await _uow.Repository<PurchaseOrder>().GetByIdAsync(poId);
        if (po == null) throw new NotFoundException("PurchaseOrder", poId);

        if (po.Status != PurchaseOrderStatus.Draft)
            throw new BusinessRuleException("Only draft purchase orders can be submitted for approval.");

        po.Status = PurchaseOrderStatus.Submitted;
        _uow.Repository<PurchaseOrder>().Update(po);
        await _uow.CommitAsync();

        await _notificationService.SendPurchaseOrderSubmittedAlertAsync(po.Id);

        return await GetPurchaseOrderByIdAsync(poId);
    }

    public async Task<PurchaseOrderResponseDto> ApprovePurchaseOrderAsync(Guid poId, PurchaseOrderApprovalDto dto)
    {
        // SECURED: Extract approver from token, preventing IDOR spoofing
        var secureApproverId = _currentUserService.UserId;
        return await ApprovePurchaseOrderAsync(poId, secureApproverId, dto.Approve);
    }

    public async Task<PurchaseOrderResponseDto> ApprovePurchaseOrderAsync(Guid poId, Guid approvedBy, bool approve)
    {
        var po = await _uow.Repository<PurchaseOrder>().GetByIdAsync(poId);
        if (po == null)
            throw new NotFoundException("PurchaseOrder", poId);

        if (po.Status != PurchaseOrderStatus.Draft && po.Status != PurchaseOrderStatus.Submitted)
            throw new BusinessRuleException("This purchase order cannot be approved or rejected in its current state.");

        var approver = await _uow.Repository<User>().GetByIdAsync(approvedBy);
        if (approver == null)
            throw new NotFoundException("User (Approver)", approvedBy);

        po.Status = approve ? PurchaseOrderStatus.Approved : PurchaseOrderStatus.Rejected;
        po.ApprovedBy = approvedBy;

        _uow.Repository<PurchaseOrder>().Update(po);
        await _uow.CommitAsync();

        // DECOUPLED: Alert the creator via MediatR background event
        if (approve)
        {
            await _publisher.Publish(new SmartInventory.Core.Events.PurchaseOrderApprovedEvent(po.Id, po.PoNumber, po.CreatedBy, approver.FullName));
        }
        else
        {
            await _publisher.Publish(new SmartInventory.Core.Events.PurchaseOrderRejectedEvent(po.Id, po.PoNumber, po.CreatedBy, approver.FullName));
        }

        return await GetPurchaseOrderByIdAsync(poId);
    }

    public async Task<PurchaseOrderResponseDto> CancelPurchaseOrderAsync(Guid poId, Guid performedBy)
    {
        var po = await _uow.Repository<PurchaseOrder>().GetByIdAsync(poId);
        if (po == null)
            throw new NotFoundException("PurchaseOrder", poId);

        if (po.Status != PurchaseOrderStatus.Draft && po.Status != PurchaseOrderStatus.Approved && po.Status != PurchaseOrderStatus.Submitted)
            throw new BusinessRuleException("Only Draft, Submitted, or Approved purchase orders can be cancelled. Partially or fully received orders cannot be cancelled directly.");

        po.Status = PurchaseOrderStatus.Cancelled;
        po.Notes += $"\n[Cancelled on {DateTime.UtcNow:yyyy-MM-dd}]";
        po.UpdatedAt = DateTime.UtcNow;

        _uow.Repository<PurchaseOrder>().Update(po);
        await _uow.CommitAsync();

        return await GetPurchaseOrderByIdAsync(poId);
    }

    public async Task<GoodsReceiptResponseDto> ReceiveGoodsAsync(GoodsReceiptCreateDto dto)
    {
        if (!string.IsNullOrEmpty(dto.IdempotencyKey))
        {
            var existingGrnId = await _cacheService.GetAsync<Guid?>($"Idempotency_GRN_{dto.IdempotencyKey}");
            if (existingGrnId != null)
            {
                return await GetGoodsReceiptByIdAsync(existingGrnId.Value);
            }
        }

        var po = await _uow.Repository<PurchaseOrder>()
            .Query()
            .Include(p => p.Items)
            .Include(p => p.Supplier)
            .Include(p => p.GoodsReceipts).ThenInclude(g => g.Items)
            .Include(p => p.SupplierInvoices)
            .Include(p => p.Shipments).ThenInclude(s => s.Items)
            .FirstOrDefaultAsync(p => p.Id == dto.PurchaseOrderId);

        if (po == null)
            throw new NotFoundException("PurchaseOrder", dto.PurchaseOrderId);

        if (po.Status != PurchaseOrderStatus.Approved && po.Status != PurchaseOrderStatus.PartiallyReceived)
            throw new BusinessRuleException("Can only receive goods for an approved or partially received purchase order.");

        bool poHasShipments = po.Shipments.Count > 0;
        if (poHasShipments && !dto.PurchaseOrderShipmentId.HasValue)
            throw new BusinessRuleException("This purchase order has supplier shipments. Select a shipment when receiving goods.");

        PurchaseOrderShipment? targetShipment = null;
        if (dto.PurchaseOrderShipmentId.HasValue)
        {
            targetShipment = po.Shipments.FirstOrDefault(s => s.Id == dto.PurchaseOrderShipmentId.Value);
            if (targetShipment == null)
                throw new NotFoundException("PurchaseOrderShipment", dto.PurchaseOrderShipmentId.Value);
        }

        // Enforcement Gate B: Mandatory Delivery Challan Evidence
        if (dto.AttachmentIds == null || !dto.AttachmentIds.Any())
            throw new BusinessRuleException("A Delivery Challan must be uploaded and attached to receive goods.");

        var attachments = await _uow.Repository<FileAttachment>()
            .Query()
            .Where(f => dto.AttachmentIds.Contains(f.Id))
            .ToListAsync();

        if (!attachments.Any(f => f.Category == DocumentCategory.DeliveryChallan))
            throw new BusinessRuleException("At least one attached file must be categorized as a Delivery Challan.");

        var grnId = Guid.NewGuid();

        // Link the attachments to the new GRN
        foreach (var attachment in attachments)
        {
            attachment.EntityType = "GoodsReceipt";
            attachment.EntityId = grnId;
            _uow.Repository<FileAttachment>().Update(attachment);
        }

        var grnItems = new List<GoodsReceiptItem>();
        bool allAccepted = true;

        foreach (var itemDto in dto.Items)
        {
            var poItem = po.Items.FirstOrDefault(i => i.Id == itemDto.PurchaseOrderItemId);
            if (poItem == null)
                throw new NotFoundException("PurchaseOrderItem", itemDto.PurchaseOrderItemId);

            var product = await _uow.Repository<Product>().GetByIdAsync(poItem.ProductId);
            if (product == null)
                throw new NotFoundException("Product", poItem.ProductId);

            if (itemDto.QuantityReceived < 0 || itemDto.QuantityRejected < 0)
                throw new BusinessRuleException($"Received and rejected quantities for Product {product.Name} cannot be negative.");

            if (itemDto.QuantityRejected > 0)
            {
                allAccepted = false;
            }

            var grnItem = new GoodsReceiptItem
            {
                Id = Guid.NewGuid(),
                GoodsReceiptId = grnId,
                PurchaseOrderItemId = itemDto.PurchaseOrderItemId,
                BinLocationId = itemDto.BinLocationId,
                QuantityReceived = itemDto.QuantityReceived,
                QuantityRejected = itemDto.QuantityRejected,
                RejectionReason = itemDto.RejectionReason
            };

            grnItems.Add(grnItem);

            // Calculate actual accepted qty added to physical stock
            int acceptedQty = itemDto.QuantityReceived - itemDto.QuantityRejected;

            if (poItem.QuantityReceived + itemDto.QuantityReceived > poItem.QuantityOrdered)
                throw new BusinessRuleException($"Cannot receive more items than ordered for Product {product.Name}. Ordered: {poItem.QuantityOrdered}, Already Received: {poItem.QuantityReceived}, Attempted: {itemDto.QuantityReceived}");

            if (targetShipment != null)
            {
                var shipmentLine = targetShipment.Items.FirstOrDefault(si => si.PurchaseOrderItemId == itemDto.PurchaseOrderItemId);
                if (shipmentLine == null)
                    throw new BusinessRuleException($"Product {product.Name} is not included on the selected shipment.");

                int alreadyReceivedOnShipment = po.GoodsReceipts
                    .Where(g => g.PurchaseOrderShipmentId == targetShipment.Id)
                    .SelectMany(g => g.Items)
                    .Where(gi => gi.PurchaseOrderItemId == itemDto.PurchaseOrderItemId)
                    .Sum(gi => gi.QuantityReceived);

                if (alreadyReceivedOnShipment + itemDto.QuantityReceived > shipmentLine.QuantityDispatched)
                    throw new BusinessRuleException(
                        $"Cannot receive more than dispatched on shipment for {product.Name}. " +
                        $"Dispatched: {shipmentLine.QuantityDispatched}, Already received on shipment: {alreadyReceivedOnShipment}, Attempted: {itemDto.QuantityReceived}");
            }

            if (acceptedQty > 0)
            {
                // ── Enterprise Capacity Engine Checks ────────────────────────────────────
                {
                    var targetBin = await _uow.Repository<BinLocation>()
                        .Query()
                        .Include(b => b.Zone)
                        .FirstOrDefaultAsync(b => b.Id == itemDto.BinLocationId);

                    if (targetBin != null)
                    {
                        decimal additionalVolume = acceptedQty * product.VolumeCm3;
                        decimal additionalWeight = acceptedQty * product.WeightKg;

                        // 1a. Volume Capacity Hard Block
                        if (targetBin.MaxVolumeCm3 > 0 && targetBin.UtilizedVolumeCm3 + additionalVolume > targetBin.MaxVolumeCm3)
                            throw new BusinessRuleException(
                                $"Bin '{targetBin.BinCode}' has insufficient volume capacity. " +
                                $"Required: {additionalVolume:F0} cm\u00b3, Available: {targetBin.MaxVolumeCm3 - targetBin.UtilizedVolumeCm3:F0} cm\u00b3. " +
                                "Please select a different bin or reduce the quantity.");

                        // 1b. Weight Capacity Hard Block
                        if (targetBin.MaxWeightKg > 0 && targetBin.UtilizedWeightKg + additionalWeight > targetBin.MaxWeightKg)
                            throw new BusinessRuleException(
                                $"Bin '{targetBin.BinCode}' has insufficient weight capacity. " +
                                $"Required: {additionalWeight:F2} kg, Available: {targetBin.MaxWeightKg - targetBin.UtilizedWeightKg:F2} kg. " +
                                "Please select a different bin or reduce the quantity.");

                        // 2. Zone/BinType Mismatch (Role-Based Soft Warning Overrides)
                        bool isZoneMismatch = targetBin.Zone.ZoneType == ZoneType.Receiving || targetBin.Zone.ZoneType == ZoneType.Shipping;
                        bool isBinTypeMismatch = product.PreferredBinType != targetBin.BinType;
                        
                        if (isZoneMismatch || isBinTypeMismatch)
                        {
                            if (!dto.BypassWarnings)
                            {
                                throw new BusinessRuleException($"Warning: Putaway validation failed (Zone or BinType mismatch). Use BypassWarnings=true to override.");
                            }
                            
                            var authResult = _currentUserService.Principal != null 
                                ? await _authorizationService.AuthorizeAsync(_currentUserService.Principal, "CanOverrideCapacity")
                                : Microsoft.AspNetCore.Authorization.AuthorizationResult.Failed();

                            if (!authResult.Succeeded)
                                throw new UnauthorizedAccessException("You do not have permission to override capacity warnings.");

                            var overrideReason = itemDto.OverrideReason ?? "Manual Override";
                            var ruleBroken = isZoneMismatch ? "ZoneMismatch" : "BinTypeMismatch";

                            var auditLog = new OverrideAuditLog
                            {
                                Id = Guid.NewGuid(),
                                UserId = _currentUserService.UserId,
                                Timestamp = DateTime.UtcNow,
                                RuleBroken = ruleBroken,
                                OverrideReason = overrideReason,
                                TargetBinId = targetBin.Id,
                                ProductId = product.Id
                            };
                            await _uow.Repository<OverrideAuditLog>().AddAsync(auditLog);

                            await _publisher.Publish(new SmartInventory.Core.Events.CapacityOverridePerformedEvent(
                                _currentUserService.UserId,
                                targetBin.Id,
                                targetBin.Barcode ?? targetBin.BinCode,
                                product.Id,
                                ruleBroken,
                                overrideReason,
                                DateTime.UtcNow
                            ));
                        }
                        
                        // 3. Increment utilized capacity
                        targetBin.UtilizedVolumeCm3 += additionalVolume;
                        targetBin.UtilizedWeightKg += additionalWeight;
                        _uow.Repository<BinLocation>().Update(targetBin);

                        if (targetBin.MaxVolumeCm3 > 0)
                        {
                            decimal utilPct = (targetBin.UtilizedVolumeCm3 / targetBin.MaxVolumeCm3) * 100m;
                            if (utilPct > 90m)
                            {
                                await _publisher.Publish(new SmartInventory.Core.Events.BinCapacityThresholdReachedEvent(
                                    targetBin.Id, targetBin.Barcode ?? targetBin.BinCode, utilPct
                                ));
                            }
                        }
                    }
                }
                // ─────────────────────────────────────────────────────────────────────────

                // -- Weighted Average Costing (WAC) --
                // DECOUPLED: Valuation logic extracted to dedicated domain service
                await _valuationService.RecalculateWacAsync(product.Id, acceptedQty, poItem.UnitPrice);
                // ------------------------------------

                // Increment PO item received quantity
                poItem.QuantityReceived += itemDto.QuantityReceived; // Keep total received on PO
                _uow.Repository<PurchaseOrderItem>().Update(poItem);

                // Update physical StockLevel
                var stockLevel = await _uow.Repository<StockLevel>()
                    .Query()
                    .FirstOrDefaultAsync(sl => sl.ProductId == poItem.ProductId &&
                                               sl.WarehouseId == dto.WarehouseId &&
                                               sl.BinLocationId == itemDto.BinLocationId);

                if (stockLevel == null)
                {
                    stockLevel = new StockLevel
                    {
                        Id = Guid.NewGuid(),
                        ProductId = poItem.ProductId,
                        WarehouseId = dto.WarehouseId,
                        BinLocationId = itemDto.BinLocationId,
                        QuantityOnHand = acceptedQty,
                        QuantityReserved = 0,
                        QuantityOnOrder = 0,
                        LastUpdated = DateTime.UtcNow
                    };
                    await _uow.Repository<StockLevel>().AddAsync(stockLevel);
                }
                else
                {
                    stockLevel.QuantityOnHand += acceptedQty;
                    stockLevel.LastUpdated = DateTime.UtcNow;
                    _uow.Repository<StockLevel>().Update(stockLevel);
                }

                // Add StockMovement record
                var movement = new StockMovement
                {
                    Id = Guid.NewGuid(),
                    ProductId = poItem.ProductId,
                    WarehouseId = dto.WarehouseId,
                    BinLocationId = itemDto.BinLocationId,
                    MovementType = MovementType.Purchase,
                    Quantity = acceptedQty,
                    ReferenceType = ReferenceType.PurchaseOrder, // Correct reference enum value
                    ReferenceId = grnId,
                    PerformedBy = _currentUserService.UserId,
                    CreatedAt = DateTime.UtcNow
                };
                await _uow.Repository<StockMovement>().AddAsync(movement);

                // Check low stock warning trigger
                if (stockLevel.QuantityOnHand <= 0)
                {
                    await _notificationService.SendOutOfStockAlertAsync(product.Id, dto.WarehouseId, stockLevel.QuantityOnHand);
                }
                else if (stockLevel.QuantityOnHand <= product.SafetyStockQty)
                {
                    await _notificationService.SendSafetyStockAlertAsync(product.Id, dto.WarehouseId, stockLevel.QuantityOnHand, product.SafetyStockQty);
                }
                else if (stockLevel.QuantityOnHand <= product.ReorderPoint)
                {
                    await _notificationService.SendLowStockAlertAsync(product.Id, dto.WarehouseId, stockLevel.QuantityOnHand, product.ReorderPoint);
                }
            }
        }

        // Determine if PO is fully received or partially received
        bool fullyReceived = true;
        foreach (var poItem in po.Items)
        {
            if (poItem.QuantityReceived < poItem.QuantityOrdered)
            {
                fullyReceived = false;
                break;
            }
        }

        po.Status = fullyReceived ? PurchaseOrderStatus.Received : PurchaseOrderStatus.PartiallyReceived;
        if (fullyReceived && po.ActualDelivery == null)
        {
            po.ActualDelivery = DateTime.UtcNow;
        }
        _uow.Repository<PurchaseOrder>().Update(po);

        var grn = new GoodsReceipt
        {
            Id = grnId,
            PurchaseOrderId = dto.PurchaseOrderId,
            PurchaseOrderShipmentId = dto.PurchaseOrderShipmentId,
            ReceivedBy = dto.ReceivedBy,
            WarehouseId = dto.WarehouseId,
            ReceivedDate = DateTime.UtcNow,
            Status = allAccepted ? GoodsReceiptStatus.Accepted : GoodsReceiptStatus.PartiallyAccepted,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow,
            Items = grnItems
        };

        await _uow.Repository<GoodsReceipt>().AddAsync(grn);
        try
        {
            await _uow.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new StaleDataException("StockLevel");
        }

        if (!string.IsNullOrEmpty(dto.IdempotencyKey))
        {
            await _cacheService.SetAsync($"Idempotency_GRN_{dto.IdempotencyKey}", grnId, TimeSpan.FromHours(24));
        }

        // Notify PO creator & Warehouse Manager
        await _notificationService.SendNotificationAsync(po.CreatedBy, NotificationChannel.InApp, "GoodsReceived", 
            "Goods Receipt Created", $"Goods receipt {grn.GrnNumber} has been recorded for Purchase Order {po.PoNumber}.", 
            "GoodsReceipt", grn.Id);

        // ── SUPPLIER NOTIFICATION: Inform supplier of accepted/rejected qty and invoiceable amount
        var supplier = po.Supplier;
        if (supplier != null)
        {
            int totalAcceptedQty = grnItems.Sum(gi => gi.QuantityReceived - gi.QuantityRejected);
            int totalRejectedQty = grnItems.Sum(gi => gi.QuantityRejected);

            var rejectionReasons = grnItems
                .Where(gi => gi.QuantityRejected > 0 && !string.IsNullOrEmpty(gi.RejectionReason))
                .Select(gi => gi.RejectionReason!)
                .Distinct()
                .ToList();
            string? combinedRejectionReasons = rejectionReasons.Any() ? string.Join("; ", rejectionReasons) : null;

            // Recalculate Aggregate Accepted GRN Value (including the newly added GRN)
            decimal aggregateAcceptedGrnValue = 0;
            var allGrns = po.GoodsReceipts.ToList();
            if (!allGrns.Any(g => g.Id == grnId))
                allGrns.Add(grn);

            foreach (var g in allGrns.Where(x => x.Status == GoodsReceiptStatus.Accepted || x.Status == GoodsReceiptStatus.PartiallyAccepted))
            {
                foreach (var gi in g.Items)
                {
                    var poItem = po.Items.FirstOrDefault(i => i.Id == gi.PurchaseOrderItemId);
                    if (poItem != null)
                    {
                        aggregateAcceptedGrnValue += (gi.QuantityReceived - gi.QuantityRejected) * poItem.UnitPrice;
                    }
                }
            }

            // Calculate existing matched/paid invoices
            decimal aggregateMatchedInvoiceValue = po.SupplierInvoices
                .Where(i => i.Status == SupplierInvoiceStatus.Matched || i.Status == SupplierInvoiceStatus.Paid)
                .Sum(i => i.ApprovedAmount ?? 0);

            decimal remainingInvoiceableAmount = aggregateAcceptedGrnValue - aggregateMatchedInvoiceValue;

            // Fire-and-forget outbox notification
            if (totalRejectedQty > 0)
            {
                _ = _notificationService.SendGoodsReceiptVarianceAlertAsync(
                    po.Id,
                    grnId,
                    totalAcceptedQty,
                    totalRejectedQty,
                    combinedRejectionReasons,
                    remainingInvoiceableAmount);
            }
        }

        return await GetGoodsReceiptByIdAsync(grnId);
    }

    public async Task<GoodsReceiptResponseDto> ReceiveGoodsByBarcodeAsync(BarcodeGoodsReceiptCreateDto dto)
    {
        if (dto.PurchaseOrderId == Guid.Empty)
            throw new BusinessRuleException("PurchaseOrderId is required for barcode-based receiving.");

        var po = await _uow.Repository<PurchaseOrder>()
            .Query()
            .Include(p => p.Items)
            .Include(p => p.Supplier)
            .Include(p => p.GoodsReceipts).ThenInclude(g => g.Items)
            .Include(p => p.SupplierInvoices)
            .Include(p => p.Shipments).ThenInclude(s => s.Items)
            .FirstOrDefaultAsync(p => p.Id == dto.PurchaseOrderId);

        if (po == null)
            throw new NotFoundException("PurchaseOrder", dto.PurchaseOrderId);

        if (po.Status != PurchaseOrderStatus.Approved && po.Status != PurchaseOrderStatus.PartiallyReceived)
            throw new BusinessRuleException("Can only receive goods for an approved or partially received purchase order.");

        bool poHasShipments = po.Shipments.Count > 0;
        if (poHasShipments && !dto.PurchaseOrderShipmentId.HasValue)
            throw new BusinessRuleException("This purchase order has supplier shipments. Select a shipment when receiving goods.");

        PurchaseOrderShipment? targetShipment = null;
        if (dto.PurchaseOrderShipmentId.HasValue)
        {
            targetShipment = po.Shipments.FirstOrDefault(s => s.Id == dto.PurchaseOrderShipmentId.Value);
            if (targetShipment == null)
                throw new NotFoundException("PurchaseOrderShipment", dto.PurchaseOrderShipmentId.Value);
        }

        var attachments = await _uow.Repository<FileAttachment>()
            .Query()
            .Where(f => dto.AttachmentIds.Contains(f.Id))
            .ToListAsync();

        if (!attachments.Any(f => f.Category == DocumentCategory.DeliveryChallan))
            throw new BusinessRuleException("At least one attached file must be categorized as a Delivery Challan.");

        var barcodeValues = dto.Items.Select(i => i.BarcodeValue).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var barcodes = await _uow.Repository<Barcode>()
            .Query()
            .Include(b => b.Product)
            .Where(b => barcodeValues.Contains(b.BarcodeValue))
            .ToListAsync();

        if (barcodes.Count != barcodeValues.Count)
        {
            var missing = barcodeValues.Except(barcodes.Select(b => b.BarcodeValue), StringComparer.OrdinalIgnoreCase);
            throw new NotFoundException("Barcode", missing.First());
        }

        var receiptItems = new List<GoodsReceiptItemDto>();
        foreach (var item in dto.Items)
        {
            var barcode = barcodes.FirstOrDefault(b => b.BarcodeValue.Equals(item.BarcodeValue, StringComparison.OrdinalIgnoreCase));
            if (barcode == null)
                throw new NotFoundException("Barcode", item.BarcodeValue);

            var poItem = po.Items.FirstOrDefault(i => i.ProductId == barcode.ProductId);
            if (poItem == null)
                throw new BusinessRuleException($"Barcode {item.BarcodeValue} does not map to a product on Purchase Order {po.PoNumber}.");

            // ─── Validation: If a shipment was selected, validate the scanned product is in that shipment ───
            if (targetShipment != null)
            {
                var shipmentLine = targetShipment.Items.FirstOrDefault(si => si.PurchaseOrderItemId == poItem.Id);
                if (shipmentLine == null)
                    throw new BusinessRuleException(
                        $"Barcode {item.BarcodeValue} (Product: {barcode.Product?.Name}) is not included on the selected shipment {targetShipment.ShipmentNumber}. " +
                        $"Verify the shipment and scanned items match.");
            }

            if (string.IsNullOrWhiteSpace(item.BinBarcode))
                throw new BusinessRuleException("BinBarcode is required when receiving goods from a barcode scan.");

            var bin = await _uow.Repository<BinLocation>()
                .Query()
                .Include(b => b.Zone)
                .ThenInclude(z => z.Warehouse)
                .FirstOrDefaultAsync(b => b.Barcode == item.BinBarcode && b.Zone.WarehouseId == dto.WarehouseId);

            if (bin == null)
                throw new NotFoundException("BinLocation", item.BinBarcode!);

            receiptItems.Add(new GoodsReceiptItemDto
            {
                PurchaseOrderItemId = poItem.Id,
                BinLocationId = bin.Id,
                QuantityReceived = item.QuantityReceived,
                QuantityRejected = item.QuantityRejected,
                RejectionReason = item.RejectionReason,
                OverrideReason = item.OverrideReason
            });
        }

        var grnDto = new GoodsReceiptCreateDto
        {
            PurchaseOrderId = dto.PurchaseOrderId,
            PurchaseOrderShipmentId = dto.PurchaseOrderShipmentId,
            ReceivedBy = dto.ReceivedBy,
            WarehouseId = dto.WarehouseId,
            Notes = dto.Notes,
            IdempotencyKey = dto.IdempotencyKey,
            BypassWarnings = dto.BypassWarnings,
            Items = receiptItems,
            AttachmentIds = dto.AttachmentIds
        };

        return await ReceiveGoodsAsync(grnDto);
    }

    public async Task<PurchaseOrderResponseDto> GetPurchaseOrderByIdAsync(Guid poId)
    {
        var po = await _uow.Repository<PurchaseOrder>()
            .Query()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.Supplier)
            .Include(p => p.Warehouse)
            .Include(p => p.CreatedByUser)
            .Include(p => p.ApprovedByUser)
            .Include(p => p.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(p => p.Id == poId);

        if (po == null)
            throw new NotFoundException("PurchaseOrder", poId);

        return po.Adapt<PurchaseOrderResponseDto>();
    }

    public async Task<PagedResult<PurchaseOrderResponseDto>> GetPurchaseOrdersAsync(PurchaseOrderQueryParameters queryParams)
    {
        var poQuery = _uow.Repository<PurchaseOrder>().Query();

        poQuery = poQuery
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.Supplier)
            .Include(p => p.Warehouse)
            .Include(p => p.CreatedByUser)
            .Include(p => p.ApprovedByUser)
            .Include(p => p.Items)
                .ThenInclude(i => i.Product);

        if (queryParams.SupplierId.HasValue)
            poQuery = poQuery.Where(p => p.SupplierId == queryParams.SupplierId.Value);

        if (queryParams.WarehouseId.HasValue)
            poQuery = poQuery.Where(p => p.WarehouseId == queryParams.WarehouseId.Value);

        if (queryParams.Status.HasValue)
            poQuery = poQuery.Where(p => p.Status == queryParams.Status.Value);

        if (queryParams.FromDate.HasValue)
            poQuery = poQuery.Where(p => p.CreatedAt >= queryParams.FromDate.Value);

        if (queryParams.ToDate.HasValue)
            poQuery = poQuery.Where(p => p.CreatedAt <= queryParams.ToDate.Value);

        if (!string.IsNullOrWhiteSpace(queryParams.Search))
        {
            poQuery = poQuery.Where(p => p.PoNumber.Contains(queryParams.Search) || 
                                         (p.Notes != null && p.Notes.Contains(queryParams.Search)));
        }

        // Sorting & Paging
        int totalCount = await poQuery.CountAsync();
        
        poQuery = queryParams.SortDir.ToLower() == "asc" 
            ? poQuery.OrderBy(p => p.CreatedAt) 
            : poQuery.OrderByDescending(p => p.CreatedAt);

        var data = await poQuery
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .ToListAsync();

        return new PagedResult<PurchaseOrderResponseDto>
        {
            Data = data.Adapt<IEnumerable<PurchaseOrderResponseDto>>(),
            TotalCount = totalCount,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }

    public async Task<PagedResult<PurchaseOrderResponseDto>> SearchPurchaseOrdersAsync(DynamicQueryRequest request)
    {
        var pagedResult = await _uow.Repository<PurchaseOrder>()
            .GetPagedDynamicAsync(request, 
                p => p.Supplier, 
                p => p.Warehouse, 
                p => p.CreatedByUser, 
                p => p.ApprovedByUser!);

        return new PagedResult<PurchaseOrderResponseDto>
        {
            Data = pagedResult.Data.Adapt<IEnumerable<PurchaseOrderResponseDto>>(),
            TotalCount = pagedResult.TotalCount,
            Page = pagedResult.Page,
            PageSize = pagedResult.PageSize
        };
    }

    private async Task<GoodsReceiptResponseDto> GetGoodsReceiptByIdAsync(Guid grnId)
    {
        var grn = await _uow.Repository<GoodsReceipt>()
            .Query()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(g => g.PurchaseOrder)
            .Include(g => g.PurchaseOrderShipment)
            .Include(g => g.ReceivedByUser)
            .Include(g => g.Warehouse)
            .Include(g => g.Items)
                .ThenInclude(i => i.PurchaseOrderItem)
                    .ThenInclude(poi => poi.Product)
            .Include(g => g.Items)
                .ThenInclude(i => i.BinLocation)
            .FirstOrDefaultAsync(g => g.Id == grnId);

        if (grn == null)
            throw new NotFoundException("GoodsReceipt", grnId);

        return new GoodsReceiptResponseDto
        {
            Id = grn.Id,
            GrnNumber = grn.GrnNumber,
            PurchaseOrderId = grn.PurchaseOrderId,
            PurchaseOrderNumber = grn.PurchaseOrder.PoNumber,
            PurchaseOrderShipmentId = grn.PurchaseOrderShipmentId,
            ShipmentNumber = grn.PurchaseOrderShipment?.ShipmentNumber,
            ReceivedBy = grn.ReceivedBy,
            ReceivedByUserName = grn.ReceivedByUser.FullName,
            WarehouseId = grn.WarehouseId,
            WarehouseName = grn.Warehouse.Name,
            ReceivedDate = grn.ReceivedDate,
            Status = grn.Status,
            Notes = grn.Notes,
            CreatedAt = grn.CreatedAt,
            Items = grn.Items.Select(i => new GoodsReceiptItemResponseDto
            {
                Id = i.Id,
                PurchaseOrderItemId = i.PurchaseOrderItemId,
                ProductId = i.PurchaseOrderItem.ProductId,
                ProductName = i.PurchaseOrderItem.Product.Name,
                ProductSKU = i.PurchaseOrderItem.Product.SKU, // Correct uppercase property
                BinLocationId = i.BinLocationId,
                BinLocationCode = i.BinLocation?.Barcode,
                QuantityReceived = i.QuantityReceived,
                QuantityRejected = i.QuantityRejected,
                RejectionReason = i.RejectionReason
            }).ToList()
        };
    }

    public async Task<IEnumerable<GoodsReceiptResponseDto>> GetGoodsReceiptsAsync(Guid poId)
    {
        var grns = await _uow.Repository<GoodsReceipt>()
            .Query()
            .Include(g => g.PurchaseOrder)
            .Include(g => g.PurchaseOrderShipment)
            .Include(g => g.ReceivedByUser)
            .Include(g => g.Warehouse)
            .Include(g => g.Items).ThenInclude(i => i.PurchaseOrderItem).ThenInclude(poi => poi.Product)
            .Include(g => g.Items).ThenInclude(i => i.BinLocation)
            .Where(g => g.PurchaseOrderId == poId)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();

        return grns.Select(grn => new GoodsReceiptResponseDto
        {
            Id = grn.Id,
            GrnNumber = grn.GrnNumber,
            PurchaseOrderId = grn.PurchaseOrderId,
            PurchaseOrderNumber = grn.PurchaseOrder.PoNumber,
            PurchaseOrderShipmentId = grn.PurchaseOrderShipmentId,
            ShipmentNumber = grn.PurchaseOrderShipment?.ShipmentNumber,
            ReceivedBy = grn.ReceivedBy,
            ReceivedByUserName = grn.ReceivedByUser.FullName,
            WarehouseId = grn.WarehouseId,
            WarehouseName = grn.Warehouse.Name,
            ReceivedDate = grn.ReceivedDate,
            Status = grn.Status,
            Notes = grn.Notes,
            CreatedAt = grn.CreatedAt,
            Items = grn.Items.Select(i => new GoodsReceiptItemResponseDto
            {
                Id = i.Id,
                PurchaseOrderItemId = i.PurchaseOrderItemId,
                ProductId = i.PurchaseOrderItem.ProductId,
                ProductName = i.PurchaseOrderItem.Product.Name,
                ProductSKU = i.PurchaseOrderItem.Product.SKU,
                BinLocationId = i.BinLocationId,
                BinLocationCode = i.BinLocation?.Barcode,
                QuantityReceived = i.QuantityReceived,
                QuantityRejected = i.QuantityRejected,
                RejectionReason = i.RejectionReason
            }).ToList()
        });
    }

    public async Task<bool> CancelGoodsReceiptAsync(Guid receiptId, Guid performedBy)
    {
        var grn = await _uow.Repository<GoodsReceipt>()
            .Query()
            .Include(g => g.Items)
                .ThenInclude(i => i.PurchaseOrderItem)
            .Include(g => g.PurchaseOrder)
            .FirstOrDefaultAsync(g => g.Id == receiptId);

        if (grn == null)
            throw new NotFoundException("GoodsReceipt", receiptId);

        if (grn.Status == GoodsReceiptStatus.Cancelled)
            throw new BusinessRuleException("This Goods Receipt is already cancelled.");

        foreach (var item in grn.Items)
        {
            int acceptedQty = item.QuantityReceived - item.QuantityRejected;

            if (acceptedQty > 0)
            {
                // Revert PO Item Received Qty
                item.PurchaseOrderItem.QuantityReceived -= item.QuantityReceived;
                if (item.PurchaseOrderItem.QuantityReceived < 0)
                    throw new BusinessRuleException($"Data corruption: Reversing GRN would result in negative received quantity for PO Item {item.PurchaseOrderItem.Id}.");
                    
                _uow.Repository<PurchaseOrderItem>().Update(item.PurchaseOrderItem);

                // Revert Physical Stock Level
                var stock = await _uow.Repository<StockLevel>()
                    .Query()
                    .FirstOrDefaultAsync(sl => sl.ProductId == item.PurchaseOrderItem.ProductId &&
                                               sl.WarehouseId == grn.WarehouseId &&
                                               sl.BinLocationId == item.BinLocationId);

                if (stock != null)
                {
                    stock.QuantityOnHand -= acceptedQty;
                    if (stock.QuantityOnHand < 0) 
                        throw new InsufficientStockException(item.PurchaseOrderItem.ProductId.ToString(), acceptedQty, stock.QuantityOnHand + acceptedQty);
                        
                    stock.LastUpdated = DateTime.UtcNow;
                    _uow.Repository<StockLevel>().Update(stock);

                    // Add Reversal StockMovement
                    var movement = new StockMovement
                    {
                        Id = Guid.NewGuid(),
                        ProductId = item.PurchaseOrderItem.ProductId,
                        WarehouseId = grn.WarehouseId,
                        BinLocationId = item.BinLocationId,
                        MovementType = MovementType.Adjustment,
                        Quantity = acceptedQty,
                        ReferenceType = ReferenceType.PurchaseOrder,
                        ReferenceId = grn.PurchaseOrderId,
                        PerformedBy = performedBy,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _uow.Repository<StockMovement>().AddAsync(movement);

                    // ── Reverse Bin Spatial Capacity ────────────────────────────────────────
                    // GRN cancellation must release the volume/weight that was consumed on receipt.
                    if (item.BinLocationId.HasValue)
                    {
                        var binForCapacity = await _uow.Repository<BinLocation>()
                            .Query().Include(b => b.Zone)
                            .FirstOrDefaultAsync(b => b.Id == item.BinLocationId.Value);

                        if (binForCapacity != null)
                        {
                            var product = await _uow.Repository<Product>()
                                .GetByIdAsync(item.PurchaseOrderItem.ProductId);

                            if (product != null)
                            {
                                binForCapacity.UtilizedVolumeCm3 = Math.Max(0,
                                    binForCapacity.UtilizedVolumeCm3 - (acceptedQty * product.VolumeCm3));
                                binForCapacity.UtilizedWeightKg = Math.Max(0,
                                    binForCapacity.UtilizedWeightKg - (acceptedQty * product.WeightKg));
                                _uow.Repository<BinLocation>().Update(binForCapacity);
                            }
                        }
                    }
                    // ─────────────────────────────────────────────────────────────────────────
                }
            }
        }

        // Revert PO Status
        bool hasAnyReceived = await _uow.Repository<GoodsReceipt>()
            .Query()
            .AnyAsync(g => g.PurchaseOrderId == grn.PurchaseOrderId && g.Id != grn.Id && g.Status != GoodsReceiptStatus.Cancelled);

        grn.PurchaseOrder.Status = hasAnyReceived ? PurchaseOrderStatus.PartiallyReceived : PurchaseOrderStatus.Approved;
        _uow.Repository<PurchaseOrder>().Update(grn.PurchaseOrder);

        grn.Status = GoodsReceiptStatus.Cancelled;
        _uow.Repository<GoodsReceipt>().Update(grn);

        try
        {
            await _uow.CommitAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new StaleDataException("StockLevel");
        }
    }
}
