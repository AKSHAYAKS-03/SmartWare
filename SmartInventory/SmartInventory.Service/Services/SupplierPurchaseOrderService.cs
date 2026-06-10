using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.DTOs.SupplierPortal;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Exceptions;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Service.Services;

/// <summary>
/// Supplier portal purchase order service.
/// SECURITY: Every method validates that the PO belongs to the authenticated supplier
/// by filtering on SupplierId extracted from JWT claims.
/// </summary>
public class SupplierPurchaseOrderService : ISupplierPurchaseOrderService
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notificationService;

    public SupplierPurchaseOrderService(IUnitOfWork uow, INotificationService notificationService)
    {
        _uow = uow;
        _notificationService = notificationService;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET MY POs
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<SupplierPOListItemDto>> GetMyPurchaseOrdersAsync(Guid supplierId)
    {
        var orders = await _uow.Repository<PurchaseOrder>().Query()
            .Include(po => po.Warehouse)
            .Where(po => po.SupplierId == supplierId)
            .OrderByDescending(po => po.CreatedAt)
            .ToListAsync();

        return orders.Select(po => new SupplierPOListItemDto(
            Id: po.Id,
            PoNumber: po.PoNumber,
            Status: po.Status,
            SupplierAccepted: po.SupplierAccepted,
            TotalAmount: po.TotalAmount,
            ExpectedDelivery: po.ExpectedDelivery,
            SupplierCommittedDeliveryDate: po.SupplierCommittedDeliveryDate,
            DispatchedAt: po.DispatchedAt,
            CreatedAt: po.CreatedAt,
            WarehouseName: po.Warehouse?.Name ?? string.Empty
        )).ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET PO DETAIL
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<SupplierPODetailDto> GetPurchaseOrderDetailAsync(Guid supplierId, Guid poId)
    {
        var po = await _uow.Repository<PurchaseOrder>().Query()
            .Include(p => p.Warehouse)
            .Include(p => p.Items).ThenInclude(i => i.Product)
            .Include(p => p.GoodsReceipts).ThenInclude(g => g.Items)
            .Include(p => p.SupplierInvoices)
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.Id == poId && p.SupplierId == supplierId);

        if (po == null)
            throw new NotFoundException("PurchaseOrder", poId);

        // ── Formula: Aggregate Accepted GRN Value ─────────────────────────────
        // SUM((QuantityReceived - QuantityRejected) × UnitPrice)
        // across all Accepted / PartiallyAccepted GRNs
        decimal aggregateAcceptedGrnValue = 0;
        var acceptedGrns = po.GoodsReceipts
            .Where(g => g.Status == GoodsReceiptStatus.Accepted || g.Status == GoodsReceiptStatus.PartiallyAccepted)
            .ToList();

        foreach (var grn in acceptedGrns)
        {
            foreach (var grnItem in grn.Items)
            {
                var poItem = po.Items.FirstOrDefault(i => i.Id == grnItem.PurchaseOrderItemId);
                if (poItem != null)
                {
                    int accepted = grnItem.QuantityReceived - grnItem.QuantityRejected;
                    aggregateAcceptedGrnValue += accepted * poItem.UnitPrice;
                }
            }
        }

        // ── Formula: Aggregate Matched Invoice Value ──────────────────────────
        // SUM(ApprovedAmount) WHERE Status IN (Matched, Paid)
        decimal aggregateMatchedInvoiceValue = po.SupplierInvoices
            .Where(i => i.Status == SupplierInvoiceStatus.Matched || i.Status == SupplierInvoiceStatus.Paid)
            .Sum(i => i.ApprovedAmount ?? 0);

        // ── Formula: Remaining Invoiceable Amount ─────────────────────────────
        // AggregateAcceptedGrnValue - AggregateMatchedInvoiceValue
        decimal remainingInvoiceableAmount = aggregateAcceptedGrnValue - aggregateMatchedInvoiceValue;

        // ── Formula: Remaining Unbilled Amount ───────────────────────────────
        // PO.TotalAmount - SUM(invoice.Amount) WHERE Status IN (Pending, UnderReview, Matched, Paid)
        decimal totalInFlightInvoiceAmount = po.SupplierInvoices
            .Where(i => i.Status == SupplierInvoiceStatus.Pending
                     || i.Status == SupplierInvoiceStatus.UnderReview
                     || i.Status == SupplierInvoiceStatus.Matched
                     || i.Status == SupplierInvoiceStatus.Paid)
            .Sum(i => i.Amount);
        decimal remainingUnbilledAmount = po.TotalAmount - totalInFlightInvoiceAmount;

        // ── Per-Line Item GRN Breakdown ───────────────────────────────────────
        // Aggregate accepted/rejected quantities and rejection reasons across all GRNs per PO item
        var lineItems = po.Items.Select(i =>
        {
            var grnItemsForThisLine = acceptedGrns
                .SelectMany(g => g.Items)
                .Where(gi => gi.PurchaseOrderItemId == i.Id)
                .ToList();

            int totalAccepted = grnItemsForThisLine.Sum(gi => gi.QuantityReceived - gi.QuantityRejected);
            int totalRejected = grnItemsForThisLine.Sum(gi => gi.QuantityRejected);
            var rejectionReasons = grnItemsForThisLine
                .Where(gi => gi.QuantityRejected > 0 && !string.IsNullOrEmpty(gi.RejectionReason))
                .Select(gi => gi.RejectionReason!)
                .Distinct()
                .ToList();

            return new SupplierPOLineItemDto(
                Id: i.Id,
                ProductName: i.Product?.Name ?? string.Empty,
                ProductSku: i.Product?.SKU ?? string.Empty,
                UnitOfMeasure: i.Product?.UnitOfMeasure.ToString() ?? string.Empty,
                QuantityOrdered: i.QuantityOrdered,
                QuantityReceived: i.QuantityReceived,
                UnitPrice: i.UnitPrice,
                AcceptedQuantity: totalAccepted,
                RejectedQuantity: totalRejected,
                RejectionReason: rejectionReasons.Count > 0 ? string.Join("; ", rejectionReasons) : null,
                InvoiceableAmount: totalAccepted * i.UnitPrice
            );
        }).ToList();

        return new SupplierPODetailDto(
            Id: po.Id,
            PoNumber: po.PoNumber,
            Status: po.Status,
            SupplierAccepted: po.SupplierAccepted,
            TotalAmount: po.TotalAmount,
            ExpectedDelivery: po.ExpectedDelivery,
            SupplierCommittedDeliveryDate: po.SupplierCommittedDeliveryDate,
            DispatchedAt: po.DispatchedAt,
            TrackingNumber: po.TrackingNumber,
            Notes: po.Notes,
            SupplierNotes: po.SupplierNotes,
            CreatedAt: po.CreatedAt,
            WarehouseName: po.Warehouse?.Name ?? string.Empty,
            Items: lineItems,
            AggregateAcceptedGrnValue: aggregateAcceptedGrnValue,
            AggregateMatchedInvoiceValue: aggregateMatchedInvoiceValue,
            RemainingInvoiceableAmount: remainingInvoiceableAmount,
            RemainingUnbilledAmount: remainingUnbilledAmount
        );
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RESPOND TO PO (ACCEPT / DECLINE)
    // ─────────────────────────────────────────────────────────────────────────

    public async Task RespondToPurchaseOrderAsync(Guid supplierId, Guid poId, SupplierRespondToPORequest request)
    {
        var po = await GetSupplierPOOrThrowAsync(supplierId, poId);

        // Only POs that are Submitted or Approved can be responded to
        if (po.Status != PurchaseOrderStatus.Submitted && po.Status != PurchaseOrderStatus.Approved)
            throw new BusinessRuleException($"Cannot respond to a PO in '{po.Status}' status. Only Submitted or Approved POs can be accepted or declined.");

        if (po.SupplierAccepted.HasValue)
            throw new BusinessRuleException("This PO has already been responded to.");

        po.SupplierAccepted = request.Accept;
        if (request.Accept && request.CommittedDeliveryDate.HasValue)
            po.SupplierCommittedDeliveryDate = request.CommittedDeliveryDate.Value;
        if (!request.Accept && !string.IsNullOrWhiteSpace(request.DeclineReason))
            po.SupplierNotes = $"[DECLINED] {request.DeclineReason}";

        _uow.Repository<PurchaseOrder>().Update(po);
        await _uow.CommitAsync();

        await _notificationService.SendSupplierPurchaseOrderResponseAlertAsync(po.Id, request.Accept, request.DeclineReason);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UPDATE EXPECTED DELIVERY DATE
    // ─────────────────────────────────────────────────────────────────────────

    public async Task UpdateExpectedDeliveryAsync(Guid supplierId, Guid poId, SupplierUpdateDeliveryDateRequest request)
    {
        var po = await GetSupplierPOOrThrowAsync(supplierId, poId);

        if (po.DispatchedAt.HasValue)
            throw new BusinessRuleException("Cannot update expected delivery date after the order has been dispatched.");

        if (po.Status == PurchaseOrderStatus.Received || po.Status == PurchaseOrderStatus.Closed || po.Status == PurchaseOrderStatus.Cancelled)
            throw new BusinessRuleException($"Cannot update a PO in '{po.Status}' status.");

        po.ExpectedDelivery = request.ExpectedDelivery;
        if (!string.IsNullOrWhiteSpace(request.SupplierNotes))
            po.SupplierNotes = request.SupplierNotes;

        _uow.Repository<PurchaseOrder>().Update(po);
        await _uow.CommitAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MARK AS DISPATCHED
    // ─────────────────────────────────────────────────────────────────────────

    public async Task MarkAsDispatchedAsync(Guid supplierId, Guid poId, SupplierMarkDispatchedRequest request)
    {
        await CreateShipmentAsync(supplierId, poId, new SupplierCreateShipmentRequest(
            null, null, request.SupplierNotes, null));
    }

    public async Task<SupplierShipmentResponseDto> CreateShipmentAsync(
        Guid supplierId, Guid poId, SupplierCreateShipmentRequest request)
    {
        var po = await _uow.Repository<PurchaseOrder>().Query()
            .Include(p => p.Items).ThenInclude(i => i.Product)
            .Include(p => p.Warehouse)
            .Include(p => p.Shipments).ThenInclude(s => s.Items)
            .FirstOrDefaultAsync(p => p.Id == poId && p.SupplierId == supplierId);

        if (po == null)
            throw new NotFoundException("PurchaseOrder", poId);

        ValidatePOShippable(po);

        var remainingByLine = ComputeRemainingDispatchQuantities(po);
        if (remainingByLine.Values.All(q => q <= 0))
            throw new BusinessRuleException("All items on this order have already been fully dispatched.");

        var linesToShip = BuildShipmentLines(request.Lines, remainingByLine, po);

        var shipmentId = Guid.NewGuid();
        var shipmentItems = linesToShip.Select(line => new PurchaseOrderShipmentItem
        {
            Id = Guid.NewGuid(),
            PurchaseOrderShipmentId = shipmentId,
            PurchaseOrderItemId = line.PurchaseOrderItemId,
            QuantityDispatched = line.QuantityDispatched,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        var shipment = new PurchaseOrderShipment
        {
            Id = shipmentId,
            TrackingNumber = null,
            CarrierName = request.CarrierName,
            DispatchedAt = DateTime.UtcNow,
            ExpectedDelivery = request.ExpectedDelivery,
            SupplierNotes = request.SupplierNotes,
            PurchaseOrderId = poId,
            CreatedAt = DateTime.UtcNow,
            Items = shipmentItems
        };

        if (!po.DispatchedAt.HasValue)
            po.DispatchedAt = shipment.DispatchedAt;
        if (!string.IsNullOrWhiteSpace(request.SupplierNotes))
            po.SupplierNotes = request.SupplierNotes;

        await _uow.Repository<PurchaseOrderShipment>().AddAsync(shipment);
        _uow.Repository<PurchaseOrder>().Update(po);
        await _uow.CommitAsync();

        var savedShipment = await _uow.Repository<PurchaseOrderShipment>().Query()
            .Include(s => s.Items)
            .FirstAsync(s => s.Id == shipmentId);

        if (po.Warehouse?.ManagerId != null)
        {
            await _notificationService.SendNotificationAsync(
                po.Warehouse.ManagerId.Value, NotificationChannel.InApp,
                "SupplierShipmentCreated", "Inbound Shipment Dispatched",
                $"Supplier dispatched shipment for PO {po.PoNumber}. Tracking: {savedShipment.TrackingNumber ?? "N/A"}.",
                "PurchaseOrderShipment", shipmentId);
        }

        if (po.TrackingNumber != savedShipment.TrackingNumber)
        {
            po.TrackingNumber = savedShipment.TrackingNumber;
            _uow.Repository<PurchaseOrder>().Update(po);
            await _uow.CommitAsync();
        }

        return MapShipmentToDto(savedShipment, po);
    }

    public async Task<List<SupplierShipmentResponseDto>> GetShipmentsAsync(Guid supplierId, Guid poId)
    {
        var po = await _uow.Repository<PurchaseOrder>().Query()
            .Include(p => p.Items).ThenInclude(i => i.Product)
            .Include(p => p.Shipments).ThenInclude(s => s.Items).ThenInclude(i => i.PurchaseOrderItem).ThenInclude(poi => poi.Product)
            .FirstOrDefaultAsync(p => p.Id == poId && p.SupplierId == supplierId);

        if (po == null)
            throw new NotFoundException("PurchaseOrder", poId);

        return po.Shipments
            .OrderByDescending(s => s.DispatchedAt)
            .Select(s => MapShipmentToDto(s, po))
            .ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PRIVATE HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    private static void ValidatePOShippable(PurchaseOrder po)
    {
        if (po.Status == PurchaseOrderStatus.Draft || po.Status == PurchaseOrderStatus.Submitted)
            throw new BusinessRuleException("Cannot dispatch an order that has not been approved yet.");

        if (po.Status == PurchaseOrderStatus.Received || po.Status == PurchaseOrderStatus.Closed || po.Status == PurchaseOrderStatus.Cancelled)
            throw new BusinessRuleException($"Cannot dispatch a PO in '{po.Status}' status.");
    }

    private static Dictionary<Guid, int> ComputeRemainingDispatchQuantities(PurchaseOrder po)
    {
        var dispatched = po.Shipments
            .SelectMany(s => s.Items)
            .GroupBy(i => i.PurchaseOrderItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.QuantityDispatched));

        return po.Items.ToDictionary(
            i => i.Id,
            i => i.QuantityOrdered - dispatched.GetValueOrDefault(i.Id, 0));
    }

    private static List<SupplierShipmentLineRequest> BuildShipmentLines(
        List<SupplierShipmentLineRequest>? requestedLines,
        Dictionary<Guid, int> remainingByLine,
        PurchaseOrder po)
    {
        if (requestedLines == null || requestedLines.Count == 0)
        {
            return remainingByLine
                .Where(kv => kv.Value > 0)
                .Select(kv => new SupplierShipmentLineRequest(kv.Key, kv.Value))
                .ToList();
        }

        var result = new List<SupplierShipmentLineRequest>();
        foreach (var line in requestedLines)
        {
            if (line.QuantityDispatched <= 0)
                throw new BusinessRuleException("Shipment line quantity must be greater than zero.");

            if (!remainingByLine.TryGetValue(line.PurchaseOrderItemId, out int remaining))
                throw new NotFoundException("PurchaseOrderItem", line.PurchaseOrderItemId);

            if (line.QuantityDispatched > remaining)
            {
                var poItem = po.Items.First(i => i.Id == line.PurchaseOrderItemId);
                throw new BusinessRuleException(
                    $"Cannot dispatch {line.QuantityDispatched} units of {poItem.Product.Name}. Only {remaining} remaining.");
            }

            result.Add(line);
        }

        return result;
    }

    private static SupplierShipmentResponseDto MapShipmentToDto(PurchaseOrderShipment shipment, PurchaseOrder po)
    {
        var lineDtos = shipment.Items.Select(i =>
        {
            var poItem = po.Items.First(p => p.Id == i.PurchaseOrderItemId);
            return new SupplierShipmentLineResponseDto(
                i.PurchaseOrderItemId,
                poItem.Product.Name,
                poItem.Product.SKU,
                i.QuantityDispatched,
                poItem.UnitPrice,
                i.QuantityDispatched * poItem.UnitPrice);
        }).ToList();

        return new SupplierShipmentResponseDto(
            Id: shipment.Id,
            ShipmentNumber: shipment.ShipmentNumber,
            TrackingNumber: shipment.TrackingNumber,
            CarrierName: shipment.CarrierName,
            DispatchedAt: shipment.DispatchedAt,
            ExpectedDelivery: shipment.ExpectedDelivery,
            SupplierNotes: shipment.SupplierNotes,
            TotalAmount: lineDtos.Sum(x => x.LineAmount),
            Items: lineDtos
        );
    }

    private async Task<PurchaseOrder> GetSupplierPOOrThrowAsync(Guid supplierId, Guid poId)
    {
        var po = await _uow.Repository<PurchaseOrder>().Query()
            .FirstOrDefaultAsync(p => p.Id == poId && p.SupplierId == supplierId);

        if (po == null)
            throw new NotFoundException("PurchaseOrder", poId);

        return po;
    }
}
