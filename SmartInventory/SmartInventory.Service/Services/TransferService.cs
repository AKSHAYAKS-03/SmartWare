using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Exceptions;
using SmartInventory.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Mapster;

using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace SmartInventory.Service.Services;

public class TransferService : ITransferService
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notificationService;
    private readonly ICacheService _cacheService;
    private readonly ICurrentUserService _currentUserService;
    private readonly MediatR.IPublisher _publisher;
    private readonly IAuthorizationService _authorizationService;
    private readonly ITransferVarianceResolver _varianceResolver;

    public TransferService(
        IUnitOfWork uow, 
        INotificationService notificationService,
        ICacheService cacheService,
        ICurrentUserService currentUserService,
        MediatR.IPublisher publisher,
        IAuthorizationService authorizationService,
        ITransferVarianceResolver varianceResolver)
    {
        _uow = uow;
        _notificationService = notificationService;
        _cacheService = cacheService;
        _currentUserService = currentUserService;
        _publisher = publisher;
        _authorizationService = authorizationService;
        _varianceResolver = varianceResolver;
    }

    public async Task<TransferResponseDto> CreateTransferAsync(TransferCreateDto dto)
    {
        if (!string.IsNullOrEmpty(dto.IdempotencyKey))
        {
            var cachedResponse = await _cacheService.GetAsync<TransferResponseDto>($"Idempotency_Transfer_{dto.IdempotencyKey}");
            if (cachedResponse != null)
            {
                return cachedResponse;
            }
        }

        if (dto.FromWarehouseId == dto.ToWarehouseId)
            throw new BusinessRuleException("Origin and destination warehouses cannot be the same.");

        var fromWh = await _uow.Repository<Warehouse>().GetByIdAsync(dto.FromWarehouseId);
        if (fromWh == null)
            throw new NotFoundException("Warehouse (Origin)", dto.FromWarehouseId);

        var toWh = await _uow.Repository<Warehouse>().GetByIdAsync(dto.ToWarehouseId);
        if (toWh == null)
            throw new NotFoundException("Warehouse (Destination)", dto.ToWarehouseId);

        var user = await _uow.Repository<User>().GetByIdAsync(dto.RequestedBy);
        if (user == null)
            throw new NotFoundException("User (Requester)", dto.RequestedBy);

        var transferId = Guid.NewGuid();

        var items = new List<TransferItem>();

        foreach (var itemDto in dto.Items)
        {
            if (itemDto.QuantityRequested <= 0)
                throw new BusinessRuleException($"Quantity requested for Product ID {itemDto.ProductId} must be greater than zero.");

            var product = await _uow.Repository<Product>().GetByIdAsync(itemDto.ProductId);
            if (product == null)
                throw new NotFoundException("Product", itemDto.ProductId);

            // Validate that Origin warehouse has enough stock
            var srcStock = await _uow.Repository<StockLevel>()
                .Query()
                .FirstOrDefaultAsync(sl => sl.ProductId == itemDto.ProductId &&
                                           sl.WarehouseId == dto.FromWarehouseId &&
                                           sl.BinLocationId == itemDto.FromBinId);

            int availableStock = srcStock == null ? 0 : srcStock.QuantityOnHand - srcStock.QuantityReserved;
            if (availableStock < itemDto.QuantityRequested)
            {
                throw new InsufficientStockException(product.Name, itemDto.QuantityRequested, availableStock);
            }

            // Reserve stock
            if (srcStock != null)
            {
                srcStock.QuantityReserved += itemDto.QuantityRequested;
                _uow.Repository<StockLevel>().Update(srcStock);
            }

            var item = new TransferItem
            {
                Id = Guid.NewGuid(),
                TransferId = transferId,
                ProductId = itemDto.ProductId,
                FromBinId = itemDto.FromBinId,
                ToBinId = itemDto.ToBinId,
                QuantityRequested = itemDto.QuantityRequested,
                QuantityDispatched = 0,
                QuantityReceived = 0
            };

            items.Add(item);
        }

        var transfer = new WarehouseTransfer
        {
            Id = transferId,
            FromWarehouseId = dto.FromWarehouseId,
            ToWarehouseId = dto.ToWarehouseId,
            RequestedBy = dto.RequestedBy,
            Status = TransferStatus.Requested,
            TransferDate = DateTime.UtcNow,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow,
            Items = items
        };

        await _uow.Repository<WarehouseTransfer>().AddAsync(transfer);
        try
        {
            await _uow.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new StaleDataException("StockLevel");
        }

        // Notify destination warehouse manager of incoming transfer request
        if (toWh.ManagerId.HasValue)
        {
            await _notificationService.SendNotificationAsync(toWh.ManagerId.Value, NotificationChannel.InApp, "TransferRequested", 
                "Incoming Transfer Request", $"New transfer request {transfer.TransferNumber} has been initiated from {fromWh.Name}.", 
                "WarehouseTransfer", transferId);
        }

        var response = await GetTransferByIdAsync(transferId);

        if (!string.IsNullOrEmpty(dto.IdempotencyKey))
        {
            await _cacheService.SetAsync($"Idempotency_Transfer_{dto.IdempotencyKey}", response, TimeSpan.FromHours(24));
        }

        return response;
    }

    public async Task<TransferResponseDto> ApproveTransferAsync(Guid transferId, TransferApprovalDto dto)
    {
        var transfer = await _uow.Repository<WarehouseTransfer>()
            .Query()
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == transferId);

        if (transfer == null)
            throw new NotFoundException("WarehouseTransfer", transferId);

        if (transfer.Status != TransferStatus.Requested)
            throw new BusinessRuleException("This transfer cannot be approved or rejected in its current state.");

        var approver = await _uow.Repository<User>().GetByIdAsync(dto.ApprovedBy);
        if (approver == null)
            throw new NotFoundException("User (Approver)", dto.ApprovedBy);

        transfer.Status = dto.Approve ? TransferStatus.Approved : TransferStatus.Rejected;
        transfer.ApprovedBy = dto.ApprovedBy;

        _uow.Repository<WarehouseTransfer>().Update(transfer);

        // If rejected, unreserve the stock immediately
        if (!dto.Approve)
        {
            foreach (var item in transfer.Items)
            {
                var srcStock = await _uow.Repository<StockLevel>()
                    .Query()
                    .FirstOrDefaultAsync(sl => sl.ProductId == item.ProductId &&
                                               sl.WarehouseId == transfer.FromWarehouseId &&
                                               sl.BinLocationId == item.FromBinId);

                if (srcStock != null)
                {
                    srcStock.QuantityReserved -= item.QuantityRequested;
                    if (srcStock.QuantityReserved < 0)
                        throw new BusinessRuleException($"Data corruption: Reserved stock for Product ID {item.ProductId} cannot be negative.");
                    
                    _uow.Repository<StockLevel>().Update(srcStock);
                }
            }
        }


        try
        {
            await _uow.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new StaleDataException("StockLevel");
        }

        // Notify creator
        await _notificationService.SendNotificationAsync(transfer.RequestedBy, NotificationChannel.InApp, 
            dto.Approve ? "TransferApproved" : "TransferRejected", 
            $"Transfer Request {(dto.Approve ? "Approved" : "Rejected")}", 
            $"Your transfer request {transfer.TransferNumber} has been {(dto.Approve ? "approved" : "rejected")} by {approver.FullName}.",
            "WarehouseTransfer", transfer.Id);

        return await GetTransferByIdAsync(transferId);
    }

    public async Task<TransferResponseDto> DispatchTransferAsync(Guid transferId, Guid performedBy)
    {
        var transfer = await _uow.Repository<WarehouseTransfer>()
            .Query()
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == transferId);

        if (transfer == null)
            throw new NotFoundException("WarehouseTransfer", transferId);

        if (transfer.Status != TransferStatus.Approved)
            throw new BusinessRuleException("Only approved transfers can be dispatched.");

        transfer.Status = TransferStatus.InTransit;

        foreach (var item in transfer.Items)
        {
            item.QuantityDispatched = item.QuantityRequested;
            _uow.Repository<TransferItem>().Update(item);

            // Deduct from origin physical stock level and subtract from reserved
            var srcStock = await _uow.Repository<StockLevel>()
                .Query()
                .FirstOrDefaultAsync(sl => sl.ProductId == item.ProductId &&
                                           sl.WarehouseId == transfer.FromWarehouseId &&
                                           sl.BinLocationId == item.FromBinId);

            var product = await _uow.Repository<Product>().GetByIdAsync(item.ProductId);
            
            if (srcStock != null)
            {
                srcStock.QuantityOnHand -= item.QuantityRequested;
                srcStock.QuantityReserved -= item.QuantityRequested;
                
                if (srcStock.QuantityOnHand < 0) 
                    throw new InsufficientStockException(product?.Name ?? item.ProductId.ToString(), item.QuantityRequested, srcStock.QuantityOnHand + item.QuantityRequested);
                
                if (srcStock.QuantityReserved < 0) 
                    throw new BusinessRuleException($"Data corruption: Reserved stock for {product?.Name} cannot be negative.");
                
                srcStock.LastUpdated = DateTime.UtcNow;
                _uow.Repository<StockLevel>().Update(srcStock);

                // Add StockMovement record (TransferOut)
                var movement = new StockMovement
                {
                    Id = Guid.NewGuid(),
                    ProductId = item.ProductId,
                    WarehouseId = transfer.FromWarehouseId,
                    BinLocationId = item.FromBinId,
                    MovementType = MovementType.TransferOut,
                    Quantity = item.QuantityRequested,
                    ReferenceType = ReferenceType.Transfer, // Correct enum value
                    ReferenceId = transferId,
                    PerformedBy = performedBy,
                    CreatedAt = DateTime.UtcNow
                };
                await _uow.Repository<StockMovement>().AddAsync(movement);

                if (product != null)
                {
                    if (srcStock.QuantityOnHand <= product.SafetyStockQty)
                    {
                        await _notificationService.SendSafetyStockAlertAsync(product.Id, transfer.FromWarehouseId, srcStock.QuantityOnHand, product.SafetyStockQty);
                    }
                    else if (srcStock.QuantityOnHand <= product.ReorderPoint)
                    {
                        await _notificationService.SendLowStockAlertAsync(product.Id, transfer.FromWarehouseId, srcStock.QuantityOnHand, product.ReorderPoint);
                    }

                    // Enterprise Fix: Release spatial capacity from the origin bin now that stock has physically left
                    if (item.FromBinId.HasValue)
                    {
                        var fromBin = await _uow.Repository<BinLocation>().GetByIdAsync(item.FromBinId.Value);
                        if (fromBin != null)
                        {
                            var volumeToRelease = item.QuantityRequested * product.VolumeCm3;
                            var weightToRelease = item.QuantityRequested * product.WeightKg;

                            if (fromBin.UtilizedVolumeCm3 < volumeToRelease)
                                throw new BusinessRuleException(
                                    $"Data corruption: Bin {fromBin.Barcode ?? fromBin.BinCode} does not contain enough utilized volume.");

                            if (fromBin.UtilizedWeightKg < weightToRelease)
                                throw new BusinessRuleException(
                                    $"Data corruption: Bin {fromBin.Barcode ?? fromBin.BinCode} does not contain enough utilized weight.");

                            fromBin.UtilizedVolumeCm3 -= volumeToRelease;
                            fromBin.UtilizedWeightKg -= weightToRelease;
                            _uow.Repository<BinLocation>().Update(fromBin);
                        }
                    }
                }
            }

            // Track In-Transit at Destination
            var destStock = await _uow.Repository<StockLevel>()
                .Query()
                .FirstOrDefaultAsync(sl => sl.ProductId == item.ProductId &&
                                           sl.WarehouseId == transfer.ToWarehouseId &&
                                           sl.BinLocationId == item.ToBinId);

            if (destStock == null)
            {
                destStock = new StockLevel
                {
                    Id = Guid.NewGuid(),
                    ProductId = item.ProductId,
                    WarehouseId = transfer.ToWarehouseId,
                    BinLocationId = item.ToBinId,
                    QuantityOnHand = 0,
                    QuantityReserved = 0,
                    QuantityOnOrder = 0,
                    QuantityInTransit = item.QuantityRequested,
                    LastUpdated = DateTime.UtcNow
                };
                await _uow.Repository<StockLevel>().AddAsync(destStock);
            }
            else
            {
                destStock.QuantityInTransit += item.QuantityRequested;
                destStock.LastUpdated = DateTime.UtcNow;
                _uow.Repository<StockLevel>().Update(destStock);
            }
        }

        _uow.Repository<WarehouseTransfer>().Update(transfer);
        try
        {
            await _uow.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new StaleDataException("StockLevel");
        }

        // Notify destination warehouse manager of in-transit dispatch
        var destWh = await _uow.Repository<Warehouse>().GetByIdAsync(transfer.ToWarehouseId);
        if (destWh != null && destWh.ManagerId.HasValue)
        {
            await _notificationService.SendNotificationAsync(destWh.ManagerId.Value, NotificationChannel.InApp, "TransferDispatched", 
                "Incoming Shipment In-Transit", $"Transfer shipment {transfer.TransferNumber} has been dispatched and is now in-transit.", 
                "WarehouseTransfer", transferId);
        }

        return await GetTransferByIdAsync(transferId);
    }

    public async Task<TransferResponseDto> ReceiveTransferAsync(Guid transferId, TransferReceiveDto dto, Guid performedBy)
    {
        var transfer = await _uow.Repository<WarehouseTransfer>()
            .Query()
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == transferId);

        if (transfer == null)
            throw new NotFoundException("WarehouseTransfer", transferId);

        if (transfer.Status != TransferStatus.InTransit)
            throw new BusinessRuleException("Can only receive transfers that are currently in-transit.");

        bool hasVariance = false;
        var varianceNotifications = new List<(Guid AdjustmentId, int VarianceQty)>();

        foreach (var item in transfer.Items)
        {
            // Find the submitted received quantity for this specific item
            var receivedDto = dto.Items.FirstOrDefault(i => i.TransferItemId == item.Id);
            int actualReceived = receivedDto?.QuantityReceived ?? 0;
            
            if (actualReceived < 0)
                throw new BusinessRuleException($"Received quantity for Product {item.ProductId} cannot be negative.");

            if (actualReceived > item.QuantityDispatched)
                throw new BusinessRuleException($"Cannot receive more than dispatched for Product {item.ProductId}.");

            item.QuantityReceived = actualReceived;
            _uow.Repository<TransferItem>().Update(item);

            // ── Enterprise Capacity Engine Checks ────────────────────────────────────
            if (actualReceived > 0 && item.ToBinId.HasValue)
            {
                var targetBin = await _uow.Repository<BinLocation>()
                    .Query()
                    .Include(b => b.Zone)
                    .FirstOrDefaultAsync(b => b.Id == item.ToBinId.Value);

                var product = await _uow.Repository<Product>().GetByIdAsync(item.ProductId);

                if (targetBin != null && product != null)
                {
                    decimal additionalVolume = actualReceived * product.VolumeCm3;
                    if (targetBin.MaxVolumeCm3 > 0 && targetBin.UtilizedVolumeCm3 + additionalVolume > targetBin.MaxVolumeCm3)
                        throw new BusinessRuleException($"CapacityExceeded: Bin {targetBin.Barcode ?? targetBin.BinCode} is at capacity. Requires: {additionalVolume} cm3, Available: {targetBin.MaxVolumeCm3 - targetBin.UtilizedVolumeCm3} cm3.");

                    bool isZoneMismatch = targetBin.Zone.ZoneType == ZoneType.Receiving || targetBin.Zone.ZoneType == ZoneType.Shipping;
                    bool isBinTypeMismatch = product.PreferredBinType != targetBin.BinType;
                    
                    if (isZoneMismatch || isBinTypeMismatch)
                    {
                        if (!dto.BypassWarnings)
                            throw new BusinessRuleException($"Warning: Putaway validation failed (Zone or BinType mismatch). Use BypassWarnings=true to override.");
                        
                        var authResult = _currentUserService.Principal != null 
                            ? await _authorizationService.AuthorizeAsync(_currentUserService.Principal, "CanOverrideCapacity")
                            : Microsoft.AspNetCore.Authorization.AuthorizationResult.Failed();

                        if (!authResult.Succeeded)
                            throw new UnauthorizedAccessException("You do not have permission to override capacity warnings.");

                        var overrideReason = receivedDto?.OverrideReason ?? "Manual Override";
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
                    
                    targetBin.UtilizedVolumeCm3 += additionalVolume;
                    targetBin.UtilizedWeightKg += actualReceived * product.WeightKg;
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

            // Increment destination physical stock level
            var destStock = await _uow.Repository<StockLevel>()
                .Query()
                .FirstOrDefaultAsync(sl => sl.ProductId == item.ProductId &&
                                           sl.WarehouseId == transfer.ToWarehouseId &&
                                           sl.BinLocationId == item.ToBinId);

            if (destStock == null)
            {
                destStock = new StockLevel
                {
                    Id = Guid.NewGuid(),
                    ProductId = item.ProductId,
                    WarehouseId = transfer.ToWarehouseId,
                    BinLocationId = item.ToBinId,
                    QuantityOnHand = actualReceived,
                    QuantityReserved = 0,
                    QuantityOnOrder = 0,
                    QuantityInTransit = 0,
                    LastUpdated = DateTime.UtcNow
                };
                await _uow.Repository<StockLevel>().AddAsync(destStock);
            }
            else
            {
               if (destStock.QuantityInTransit < item.QuantityDispatched)
                {
                    throw new BusinessRuleException(
                        $"Transit quantity mismatch. InTransit={destStock.QuantityInTransit}, Dispatched={item.QuantityDispatched}");
                }

                destStock.QuantityOnHand += actualReceived;
                destStock.QuantityInTransit -= actualReceived;
                destStock.LastUpdated = DateTime.UtcNow;
                _uow.Repository<StockLevel>().Update(destStock);
            }

            // 1. Add StockMovement record for actual received (TransferIn)
            if (actualReceived > 0)
            {
                var movement = new StockMovement
                {
                    Id = Guid.NewGuid(),
                    ProductId = item.ProductId,
                    WarehouseId = transfer.ToWarehouseId,
                    BinLocationId = item.ToBinId,
                    MovementType = MovementType.TransferIn,
                    Quantity = actualReceived,
                    ReferenceType = ReferenceType.Transfer,
                    ReferenceId = transferId,
                    PerformedBy = performedBy,
                    CreatedAt = DateTime.UtcNow
                };
                await _uow.Repository<StockMovement>().AddAsync(movement);
            }

            // 2. Handle Variance (Lost in Transit) via pending StockAdjustment
            int variance = item.QuantityDispatched - actualReceived;
            if (variance > 0)
            {
                hasVariance = true;
                var adjustmentId = Guid.NewGuid();
                var adjustment = new StockAdjustment
                {
                    Id = adjustmentId,
                    AdjustmentNumber = "TEMP", // Trigger will auto-generate
                    ProductId = item.ProductId,
                    WarehouseId = transfer.ToWarehouseId,
                    BinLocationId = item.ToBinId,
                    Reason = AdjustmentReason.LossInTransit,
                    ShrinkageReason = ShrinkageReason.HandlingLoss,
                    ReferenceType = ReferenceType.Transfer,
                    ReferenceId = transferId,
                    QuantityBefore = actualReceived,
                    QuantityAfter = actualReceived,
                    QuantityChange = -variance,
                    Status = AdjustmentStatus.Pending,
                    Notes = $"Transit Variance on Transfer {transfer.TransferNumber}. Missing {variance} units.",
                    PerformedBy = performedBy,
                    CreatedAt = DateTime.UtcNow
                };
                await _uow.Repository<StockAdjustment>().AddAsync(adjustment);
                varianceNotifications.Add((adjustmentId, variance));
            }
        }

        transfer.Status = hasVariance ? TransferStatus.ReceivedWithVariance : TransferStatus.Received;
        if (hasVariance)
            transfer.VarianceResolutionStatus = TransferVarianceResolutionStatus.PendingApproval;
        _uow.Repository<WarehouseTransfer>().Update(transfer);
        try
        {
            await _uow.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new StaleDataException("StockLevel");
        }

        if (hasVariance)
        {
            await _notificationService.SendNotificationAsync(transfer.RequestedBy, NotificationChannel.InApp,
                "TransferReceivedWithVariance", "Transfer Received with Variance",
                $"Transfer {transfer.TransferNumber} was received with quantity shortfalls. Manager approval is required for transit loss adjustments.",
                "WarehouseTransfer", transferId);

            foreach (var (adjustmentId, varianceQty) in varianceNotifications)
            {
                await _varianceResolver.NotifyVarianceCreatedAsync(
                    transferId, adjustmentId, varianceQty, transfer.TransferNumber, transfer.ToWarehouseId);
            }
        }
        else
        {
            await _notificationService.SendNotificationAsync(transfer.RequestedBy, NotificationChannel.InApp,
                "TransferReceived", "Transfer Received Successfully",
                $"Transfer shipment {transfer.TransferNumber} has been received successfully at the destination facility.",
                "WarehouseTransfer", transferId);
        }

        return await GetTransferByIdAsync(transferId);
    }

    public async Task<TransferResponseDto> GetTransferByIdAsync(Guid transferId, Guid? currentWarehouseId = null)
    {
        var transfer = await _uow.Repository<WarehouseTransfer>()
            .Query()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .Include(t => t.RequestedByUser)
            .Include(t => t.ApprovedByUser)
            .Include(t => t.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(t => t.Id == transferId);

        if (transfer == null)
            throw new NotFoundException("WarehouseTransfer", transferId);

        if (currentWarehouseId.HasValue && 
            transfer.FromWarehouseId != currentWarehouseId.Value && 
            transfer.ToWarehouseId != currentWarehouseId.Value)
        {
            throw new UnauthorizedAccessException("You do not have access to this transfer.");
        }

        var dto = transfer.Adapt<TransferResponseDto>();
        await EnrichTransferDtoAsync(dto, transfer);
        return dto;
    }

    public async Task<PagedResult<TransferResponseDto>> GetTransfersAsync(TransferQueryParameters queryParams)
    {
        var query = _uow.Repository<WarehouseTransfer>().Query();

        query = query
            .AsNoTracking()
            .AsSplitQuery()
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .Include(t => t.RequestedByUser)
            .Include(t => t.ApprovedByUser)
            .Include(t => t.Items)
                .ThenInclude(i => i.Product);

        if (queryParams.FromWarehouseId.HasValue)
            query = query.Where(t => t.FromWarehouseId == queryParams.FromWarehouseId.Value);

        if (queryParams.ToWarehouseId.HasValue)
            query = query.Where(t => t.ToWarehouseId == queryParams.ToWarehouseId.Value);

        if (queryParams.WarehouseId.HasValue)
        {
            var whId = queryParams.WarehouseId.Value;
            query = query.Where(t => t.FromWarehouseId == whId || t.ToWarehouseId == whId);
        }

        if (queryParams.Status.HasValue)
            query = query.Where(t => t.Status == queryParams.Status.Value);

        if (!string.IsNullOrWhiteSpace(queryParams.Search))
        {
            query = query.Where(t => t.TransferNumber.Contains(queryParams.Search) ||
                                     (t.Notes != null && t.Notes.Contains(queryParams.Search)));
        }

        // Sorting & Paging
        int totalCount = await query.CountAsync();

        query = queryParams.SortDir.ToLower() == "asc"
            ? query.OrderBy(t => t.CreatedAt)
            : query.OrderByDescending(t => t.CreatedAt);

        var data = await query
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .ToListAsync();

        var dtos = data.Adapt<List<TransferResponseDto>>();
        foreach (var (entity, dto) in data.Zip(dtos))
            await EnrichTransferDtoAsync(dto, entity);

        return new PagedResult<TransferResponseDto>
        {
            Data = dtos,
            TotalCount = totalCount,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }

    private async Task EnrichTransferDtoAsync(TransferResponseDto dto, WarehouseTransfer transfer)
    {
        var adjustments = await _uow.Repository<StockAdjustment>()
            .Query()
            .AsNoTracking()
            .Where(a => a.ReferenceType == ReferenceType.Transfer
                     && a.ReferenceId == transfer.Id
                     && a.Reason == AdjustmentReason.LossInTransit)
            .ToListAsync();

        var adjByProduct = adjustments.ToLookup(a => a.ProductId);

        decimal totalLoss = 0;
        int pendingCount = 0;
        int totalVariance = 0;

        foreach (var itemDto in dto.Items)
        {
            var itemVariance = Math.Max(0, itemDto.QuantityDispatched - itemDto.QuantityReceived);
            totalVariance += itemVariance;

            var adj = adjByProduct[itemDto.ProductId].FirstOrDefault();
            if (adj != null)
            {
                itemDto.VarianceAdjustmentId = adj.Id;
                itemDto.VarianceAdjustmentStatus = adj.Status;
                if (adj.Status == AdjustmentStatus.Pending)
                    pendingCount++;
            }

            var product = transfer.Items.FirstOrDefault(i => i.Id == itemDto.Id)?.Product;
            if (product != null && itemVariance > 0)
                totalLoss += itemVariance * product.CostPrice;
        }

        dto.PendingVarianceCount = pendingCount;
        dto.TotalVarianceQuantity = totalVariance;
        dto.TotalEstimatedLossValue = totalLoss;
        dto.VarianceResolutionStatus = transfer.VarianceResolutionStatus;
        dto.VarianceResolvedAt = transfer.VarianceResolvedAt;
    }

    public async Task<PagedResult<TransferResponseDto>> SearchTransfersAsync(DynamicQueryRequest request)
    {
        var pagedResult = await _uow.Repository<WarehouseTransfer>()
            .GetPagedDynamicAsync(request, 
                t => t.FromWarehouse, 
                t => t.ToWarehouse, 
                t => t.RequestedByUser);

        return new PagedResult<TransferResponseDto>
        {
            Data = pagedResult.Data.Adapt<IEnumerable<TransferResponseDto>>(),
            TotalCount = pagedResult.TotalCount,
            Page = pagedResult.Page,
            PageSize = pagedResult.PageSize
        };
    }

    public async Task<bool> TransferBinToBinAsync(BinTransferCreateDto dto, Guid performedBy)
    {
        if (dto.FromBinId == dto.ToBinId)
            throw new BusinessRuleException("Origin and destination bins cannot be the same.");

        if (dto.Quantity <= 0)
            throw new BusinessRuleException("Transfer quantity must be greater than zero.");

        var fromStock = await _uow.Repository<StockLevel>()
            .Query()
            .FirstOrDefaultAsync(sl => sl.ProductId == dto.ProductId && 
                                       sl.WarehouseId == dto.WarehouseId && 
                                       sl.BinLocationId == dto.FromBinId);

        if (fromStock == null)
            throw new NotFoundException("StockLevel (Origin)", dto.FromBinId);

        int availableStock = fromStock.QuantityOnHand - fromStock.QuantityReserved;
        if (availableStock < dto.Quantity)
            throw new InsufficientStockException("Product", dto.Quantity, availableStock);

        var toStock = await _uow.Repository<StockLevel>()
            .Query()
            .FirstOrDefaultAsync(sl => sl.ProductId == dto.ProductId && 
                                       sl.WarehouseId == dto.WarehouseId && 
                                       sl.BinLocationId == dto.ToBinId);

        // ── Enterprise Capacity Engine Checks ────────────────────────────────────
        var fromBin = await _uow.Repository<BinLocation>().GetByIdAsync(dto.FromBinId);
        var toBin = await _uow.Repository<BinLocation>().Query().Include(b => b.Zone).FirstOrDefaultAsync(b => b.Id == dto.ToBinId);
        var product = await _uow.Repository<Product>().GetByIdAsync(dto.ProductId);

        if (toBin != null && product != null)
        {
            decimal transferVolume = dto.Quantity * product.VolumeCm3;
            decimal transferWeight = dto.Quantity * product.WeightKg;

            if (toBin.MaxVolumeCm3 > 0 && toBin.UtilizedVolumeCm3 + transferVolume > toBin.MaxVolumeCm3)
                throw new BusinessRuleException($"CapacityExceeded: Target bin {toBin.Barcode ?? toBin.BinCode} is at capacity.");

            bool isZoneMismatch = toBin.Zone.ZoneType == ZoneType.Receiving || toBin.Zone.ZoneType == ZoneType.Shipping;
            bool isBinTypeMismatch = product.PreferredBinType != toBin.BinType;

            if (isZoneMismatch || isBinTypeMismatch)
            {
                if (!dto.BypassWarnings)
                    throw new BusinessRuleException($"Warning: Putaway validation failed (Zone or BinType mismatch). Use BypassWarnings=true to override.");
                
                var authResult = _currentUserService.Principal != null 
                    ? await _authorizationService.AuthorizeAsync(_currentUserService.Principal, "CanOverrideCapacity")
                    : Microsoft.AspNetCore.Authorization.AuthorizationResult.Failed();

                if (!authResult.Succeeded)
                    throw new UnauthorizedAccessException("You do not have permission to override capacity warnings.");

                var overrideReason = dto.OverrideReason ?? "Manual Override";
                var ruleBroken = isZoneMismatch ? "ZoneMismatch" : "BinTypeMismatch";

                var auditLog = new OverrideAuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = _currentUserService.UserId,
                    Timestamp = DateTime.UtcNow,
                    RuleBroken = ruleBroken,
                    OverrideReason = overrideReason,
                    TargetBinId = toBin.Id,
                    ProductId = product.Id
                };
                await _uow.Repository<OverrideAuditLog>().AddAsync(auditLog);

                await _publisher.Publish(new SmartInventory.Core.Events.CapacityOverridePerformedEvent(
                    _currentUserService.UserId,
                    toBin.Id,
                    toBin.Barcode ?? toBin.BinCode,
                    product.Id,
                    ruleBroken,
                    overrideReason,
                    DateTime.UtcNow
                ));
            }

            toBin.UtilizedVolumeCm3 += transferVolume;
            toBin.UtilizedWeightKg += transferWeight;
            _uow.Repository<BinLocation>().Update(toBin);

            if (toBin.MaxVolumeCm3 > 0)
            {
                decimal utilPct = (toBin.UtilizedVolumeCm3 / toBin.MaxVolumeCm3) * 100m;
                if (utilPct > 90m)
                {
                    await _publisher.Publish(new SmartInventory.Core.Events.BinCapacityThresholdReachedEvent(
                        toBin.Id, toBin.Barcode ?? toBin.BinCode, utilPct
                    ));
                }
            }

            if (fromBin != null)
            {
                fromBin.UtilizedVolumeCm3 -= transferVolume;
                fromBin.UtilizedWeightKg -= transferWeight;
                if (fromBin.UtilizedVolumeCm3 < 0) 
                    throw new BusinessRuleException($"Data corruption: Bin {fromBin.Barcode ?? fromBin.BinCode} has negative utilized volume.");
                if (fromBin.UtilizedWeightKg < 0) 
                    throw new BusinessRuleException($"Data corruption: Bin {fromBin.Barcode ?? fromBin.BinCode} has negative utilized weight.");
                _uow.Repository<BinLocation>().Update(fromBin);
            }
        }
        // ─────────────────────────────────────────────────────────────────────────

        if (toStock == null)
        {
            toStock = new StockLevel
            {
                Id = Guid.NewGuid(),
                ProductId = dto.ProductId,
                WarehouseId = dto.WarehouseId,
                BinLocationId = dto.ToBinId,
                QuantityOnHand = 0,
                QuantityReserved = 0,
                QuantityOnOrder = 0,
                LastUpdated = DateTime.UtcNow
            };
            await _uow.Repository<StockLevel>().AddAsync(toStock);
        }

        fromStock.QuantityOnHand -= dto.Quantity;
        fromStock.LastUpdated = DateTime.UtcNow;
        _uow.Repository<StockLevel>().Update(fromStock);

        toStock.QuantityOnHand += dto.Quantity;
        toStock.LastUpdated = DateTime.UtcNow;
        _uow.Repository<StockLevel>().Update(toStock);

        // Record stock movements
        var outMovement = new StockMovement
        {
            Id = Guid.NewGuid(),
            ProductId = dto.ProductId,
            WarehouseId = dto.WarehouseId,
            BinLocationId = dto.FromBinId,
            MovementType = MovementType.TransferOut,
            Quantity = dto.Quantity,
            ReferenceType = ReferenceType.Transfer,
            ReferenceId = Guid.Empty, // Internal transfer
            PerformedBy = performedBy,
            CreatedAt = DateTime.UtcNow
        };

        var inMovement = new StockMovement
        {
            Id = Guid.NewGuid(),
            ProductId = dto.ProductId,
            WarehouseId = dto.WarehouseId,
            BinLocationId = dto.ToBinId,
            MovementType = MovementType.TransferIn,
            Quantity = dto.Quantity,
            ReferenceType = ReferenceType.Transfer,
            ReferenceId = Guid.Empty,
            PerformedBy = performedBy,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Repository<StockMovement>().AddAsync(outMovement);
        await _uow.Repository<StockMovement>().AddAsync(inMovement);

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
