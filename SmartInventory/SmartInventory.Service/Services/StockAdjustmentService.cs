using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Exceptions;
using SmartInventory.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace SmartInventory.Service.Services;

public class StockAdjustmentService : IStockAdjustmentService
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notificationService;
    private readonly ILogger<StockAdjustmentService> _logger;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;
    private readonly MediatR.IPublisher _publisher;
    private readonly IAuthorizationService _authorizationService;
    private readonly ITransferVarianceResolver _varianceResolver;

    public StockAdjustmentService(
        IUnitOfWork uow,
        INotificationService notificationService,
        ILogger<StockAdjustmentService> logger,
        ICurrentUserService currentUserService,
        ICacheService cacheService,
        MediatR.IPublisher publisher,
        IAuthorizationService authorizationService,
        ITransferVarianceResolver varianceResolver)
    {
        _uow = uow;
        _notificationService = notificationService;
        _logger = logger;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
        _publisher = publisher;
        _authorizationService = authorizationService;
        _varianceResolver = varianceResolver;
    }

    public async Task<StockAdjustmentResponseDto> CreateAdjustmentAsync(StockAdjustmentCreateDto dto)
    {
        if (!string.IsNullOrEmpty(dto.IdempotencyKey))
        {
            var cachedResponse = await _cacheService.GetAsync<StockAdjustmentResponseDto>($"Idempotency_Adj_{dto.IdempotencyKey}");
            if (cachedResponse != null)
            {
                _logger.LogInformation("Idempotency hit for Adjustment. Key: {Key}", dto.IdempotencyKey);
                return cachedResponse;
            }
        }

        //  Validate target references exist
        if (dto.QuantityAfter < 0)
            throw new BusinessRuleException("Quantity after adjustment cannot be negative.");

        var product = await _uow.Repository<Product>().GetByIdAsync(dto.ProductId);
        if (product == null)
            throw new NotFoundException("Product", dto.ProductId);

        var warehouse = await _uow.Repository<Warehouse>().GetByIdAsync(dto.WarehouseId);
        if (warehouse == null)
            throw new NotFoundException("Warehouse", dto.WarehouseId);

        var currentUserId = _currentUserService.UserId;
        var user = await _uow.Repository<User>().GetByIdAsync(currentUserId);
        if (user == null)
            throw new NotFoundException("User", currentUserId);

        if (dto.BinLocationId.HasValue)
        {
            var bin = await _uow.Repository<BinLocation>().GetByIdAsync(dto.BinLocationId.Value);
            if (bin == null)
                throw new NotFoundException("BinLocation", dto.BinLocationId.Value);
        }

        // Fetch or assume stock level
        var stockLevel = await _uow.Repository<StockLevel>()
            .Query()
            .FirstOrDefaultAsync(sl => sl.ProductId == dto.ProductId &&
                                       sl.WarehouseId == dto.WarehouseId &&
                                       sl.BinLocationId == dto.BinLocationId);

        int currentQty = stockLevel?.QuantityOnHand ?? 0;

        // Ensure we use the actual current system qty in the adjustment record
        int qtyBefore = currentQty;
        int qtyChange = dto.QuantityAfter - qtyBefore;
        int absChange = Math.Abs(qtyChange);

        // Compute variance threshold metrics
        double percentageVariance = qtyBefore > 0 ? ((double)absChange / qtyBefore) * 100.0 : 100.0;
        decimal valueVariance = absChange * product.CostPrice;

        // If variance is > 5% and qtyBefore > 0 OR valuation is > $100
        bool requiresApproval = (percentageVariance > 5.0 && qtyBefore > 0) || (valueVariance > 100m);

        var adjustmentId = Guid.NewGuid();
        var adjustment = new StockAdjustment
        {
            Id = adjustmentId,
            ProductId = dto.ProductId,
            WarehouseId = dto.WarehouseId,
            BinLocationId = dto.BinLocationId,
            Reason = dto.Reason,
            QuantityBefore = qtyBefore,
            QuantityAfter = dto.QuantityAfter,
            QuantityChange = qtyChange,
            Status = requiresApproval ? AdjustmentStatus.Pending : AdjustmentStatus.Approved,
            Notes = dto.Notes,
            PerformedBy = currentUserId,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Repository<StockAdjustment>().AddAsync(adjustment);

        // Capacity Engine Checks (Validation & Auto-Approve)
        if (dto.BinLocationId.HasValue && qtyChange != 0)
        {
            var targetBin = await _uow.Repository<BinLocation>()
                .Query()
                .Include(b => b.Zone)
                .FirstOrDefaultAsync(b => b.Id == dto.BinLocationId.Value);

            if (targetBin != null)
            {
                decimal volumeChange = qtyChange * product.VolumeCm3;
                decimal weightChange = qtyChange * product.WeightKg;

                // Validate capacity before allowing creation/pending
                if (qtyChange > 0)
                {
                    if (targetBin.MaxVolumeCm3 > 0 && targetBin.UtilizedVolumeCm3 + volumeChange > targetBin.MaxVolumeCm3)
                        throw new BusinessRuleException($"CapacityExceeded: Bin {targetBin.Barcode ?? targetBin.BinCode} is at capacity.");

                    bool isZoneMismatch = targetBin.Zone.ZoneType == ZoneType.Receiving || targetBin.Zone.ZoneType == ZoneType.Shipping;
                    bool isBinTypeMismatch = product.PreferredBinType != targetBin.BinType;

                    if (isZoneMismatch || isBinTypeMismatch)
                    {
                        if (!dto.BypassWarnings)
                            throw new BusinessRuleException($"Warning: Validation failed (Zone or BinType mismatch). Use BypassWarnings=true to override.");
                        
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
                }

                // If auto-approved, actually update the Bin capacity
                if (!requiresApproval)
                {
                    targetBin.UtilizedVolumeCm3 += volumeChange;
                    targetBin.UtilizedWeightKg += weightChange;
                    if (targetBin.UtilizedVolumeCm3 < 0) 
                        throw new BusinessRuleException($"Data corruption: Bin {targetBin.Barcode ?? targetBin.BinCode} has negative utilized volume.");
                    if (targetBin.UtilizedWeightKg < 0) 
                        throw new BusinessRuleException($"Data corruption: Bin {targetBin.Barcode ?? targetBin.BinCode} has negative utilized weight.");
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
        }
       
        if (!requiresApproval)
        {
            // Auto approve: Update physical stock level immediately
            if (stockLevel == null)
            {
                stockLevel = new StockLevel
                {
                    Id = Guid.NewGuid(),
                    ProductId = dto.ProductId,
                    WarehouseId = dto.WarehouseId,
                    BinLocationId = dto.BinLocationId,
                    QuantityOnHand = dto.QuantityAfter,
                    QuantityReserved = 0,
                    QuantityOnOrder = 0,
                    LastUpdated = DateTime.UtcNow
                };
                await _uow.Repository<StockLevel>().AddAsync(stockLevel);
            }
            else
            {
                stockLevel.QuantityOnHand = dto.QuantityAfter;
                stockLevel.LastUpdated = DateTime.UtcNow;
                _uow.Repository<StockLevel>().Update(stockLevel);
            }

            // Append stock movement log
            var movement = new StockMovement
            {
                Id = Guid.NewGuid(),
                ProductId = dto.ProductId,
                WarehouseId = dto.WarehouseId,
                BinLocationId = dto.BinLocationId,
                MovementType = MovementType.Adjustment,
                Quantity = absChange,
                ReferenceType = ReferenceType.Adjustment, // Correct ReferenceType
                ReferenceId = adjustmentId,
                PerformedBy = currentUserId,
                CreatedAt = DateTime.UtcNow
            };
            await _uow.Repository<StockMovement>().AddAsync(movement);
            
            // Check low stock trigger
            if (stockLevel.QuantityOnHand <= 0)
            {
                await _notificationService.SendOutOfStockAlertAsync(dto.ProductId, dto.WarehouseId, stockLevel.QuantityOnHand);
            }
            else if (stockLevel.QuantityOnHand <= product.SafetyStockQty)
            {
                await _notificationService.SendSafetyStockAlertAsync(dto.ProductId, dto.WarehouseId, stockLevel.QuantityOnHand, product.SafetyStockQty);
            }
            else if (stockLevel.QuantityOnHand <= product.ReorderPoint)
            {
                await _notificationService.SendLowStockAlertAsync(dto.ProductId, dto.WarehouseId, stockLevel.QuantityOnHand, product.ReorderPoint);
            }
        }

        // Commit all changes in a single transaction
        try
        {
            await _uow.CommitAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict detected during stock adjustment creation for Product {ProductId} in Warehouse {WarehouseId}", dto.ProductId, dto.WarehouseId);
            var currentStock = await _uow.Repository<StockLevel>().Query().AsNoTracking()
                .Where(sl => sl.ProductId == dto.ProductId && sl.WarehouseId == dto.WarehouseId && sl.BinLocationId == dto.BinLocationId)
                .Select(sl => (int?)sl.QuantityOnHand)
                .FirstOrDefaultAsync();
            throw new StaleDataException("StockLevel", currentStock);
        }

        var response = MapAdjustmentToResponseDto(adjustment, product, warehouse, user, null);

        if (requiresApproval)
        {
            _logger.LogInformation("Adjustment {AdjNumber} blocked: variance exceeds thresholds. Percentage: {Pct:F2}%, Value: ${Val:F2}", 
                adjustment.AdjustmentNumber, percentageVariance, valueVariance);
            await _notificationService.SendPendingStockAdjustmentApprovalAlertAsync(adjustmentId);
            
            // Throw ApprovalRequiredException to return 202 to caller
            throw new ApprovalRequiredException($"Stock Adjustment ({adjustment.AdjustmentNumber}) due to high variance ({percentageVariance:F1}% or ${valueVariance:F2})");
        }

        if (!string.IsNullOrEmpty(dto.IdempotencyKey))
        {
            await _cacheService.SetAsync($"Idempotency_Adj_{dto.IdempotencyKey}", response, TimeSpan.FromHours(24));
        }

        return response;
    }

    public async Task<StockAdjustmentResponseDto> ApproveAdjustmentAsync(Guid adjustmentId, StockAdjustmentApprovalDto dto)
    {
        var adjustment = await _uow.Repository<StockAdjustment>().GetByIdAsync(adjustmentId);
        if (adjustment == null)
            throw new NotFoundException("StockAdjustment", adjustmentId);

        if (adjustment.Status != AdjustmentStatus.Pending)
            throw new BusinessRuleException("This stock adjustment is not in a pending state.");

        var secureApproverId = _currentUserService.UserId;

        var approver = await _uow.Repository<User>().Query().Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == secureApproverId);
        if (approver == null)
            throw new NotFoundException("User (Approver)", secureApproverId);

        if (approver.Role == null || (!string.Equals(approver.Role.Name, "Admin", StringComparison.OrdinalIgnoreCase) && !string.Equals(approver.Role.Name, "Manager", StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessRuleException("Only users with Admin or Manager roles can approve stock adjustments.");
        }

        if (adjustment.PerformedBy == secureApproverId && !string.Equals(approver.Role.Name, "Admin", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Separation of Duties (SoD) Policy: You cannot approve your own stock adjustment.");

        var product = await _uow.Repository<Product>().GetByIdAsync(adjustment.ProductId);
        var warehouse = await _uow.Repository<Warehouse>().GetByIdAsync(adjustment.WarehouseId);
        var performer = await _uow.Repository<User>().GetByIdAsync(adjustment.PerformedBy);

        if (dto.Approve)
        {
            if (adjustment.Reason == AdjustmentReason.Damage || 
                adjustment.Reason == AdjustmentReason.Theft || 
                adjustment.Reason == AdjustmentReason.WriteOff)
            {
                var hasEvidence = await _uow.Repository<FileAttachment>()
                    .Query()
                    .AnyAsync(f => f.EntityType == "StockAdjustment" && 
                                   f.EntityId == adjustment.Id && 
                                   f.Category == DocumentCategory.DamageEvidence &&
                                   f.IsVerified == true);

                if (!hasEvidence)
                    throw new BusinessRuleException($"Photographic damage/theft evidence must be attached and VERIFIED by a manager to approve a {adjustment.Reason} write-off.");
            }

            adjustment.Status = AdjustmentStatus.Approved;
            
            //Capacity Engine Checks (Manual Approve)
            if (adjustment.BinLocationId.HasValue && adjustment.QuantityChange != 0)
            {
                var targetBin = await _uow.Repository<BinLocation>()
                    .Query()
                    .Include(b => b.Zone)
                    .FirstOrDefaultAsync(b => b.Id == adjustment.BinLocationId.Value);

                if (targetBin != null && product != null)
                {
                    decimal volumeChange = adjustment.QuantityChange * product.VolumeCm3;
                    decimal weightChange = adjustment.QuantityChange * product.WeightKg;

                    if (adjustment.QuantityChange > 0)
                    {
                        if (targetBin.MaxVolumeCm3 > 0 && targetBin.UtilizedVolumeCm3 + volumeChange > targetBin.MaxVolumeCm3)
                            throw new BusinessRuleException($"CapacityExceeded: Bin {targetBin.Barcode ?? targetBin.BinCode} is at capacity. Cannot approve.");
                        
                    }

                    targetBin.UtilizedVolumeCm3 += volumeChange;
                    targetBin.UtilizedWeightKg += weightChange;
                    if (targetBin.UtilizedVolumeCm3 < 0) 
                        throw new BusinessRuleException($"Data corruption: Bin {targetBin.Barcode ?? targetBin.BinCode} has negative utilized volume.");
                    if (targetBin.UtilizedWeightKg < 0) 
                        throw new BusinessRuleException($"Data corruption: Bin {targetBin.Barcode ?? targetBin.BinCode} has negative utilized weight.");
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
          

            // Update the physical stock level
            var stockLevel = await _uow.Repository<StockLevel>()
                .Query()
                .FirstOrDefaultAsync(sl => sl.ProductId == adjustment.ProductId &&
                                           sl.WarehouseId == adjustment.WarehouseId &&
                                           sl.BinLocationId == adjustment.BinLocationId);

            if (stockLevel == null)
            {
                stockLevel = new StockLevel
                {
                    Id = Guid.NewGuid(),
                    ProductId = adjustment.ProductId,
                    WarehouseId = adjustment.WarehouseId,
                    BinLocationId = adjustment.BinLocationId,
                    QuantityOnHand = adjustment.Reason == AdjustmentReason.LossInTransit ? 0 : adjustment.QuantityAfter,
                    QuantityReserved = 0,
                    QuantityOnOrder = 0,
                    QuantityInTransit = 0,
                    LastUpdated = DateTime.UtcNow
                };
                await _uow.Repository<StockLevel>().AddAsync(stockLevel);
            }
            else
            {
                if (adjustment.Reason == AdjustmentReason.LossInTransit)
                {
                    stockLevel.QuantityInTransit -= Math.Abs(adjustment.QuantityChange);
                }
                else
                {
                    stockLevel.QuantityOnHand = adjustment.QuantityAfter;
                }
                stockLevel.LastUpdated = DateTime.UtcNow;
                _uow.Repository<StockLevel>().Update(stockLevel);
            }

            // Append stock movement log
            var movement = new StockMovement
            {
                Id = Guid.NewGuid(),
                ProductId = adjustment.ProductId,
                WarehouseId = adjustment.WarehouseId,
                BinLocationId = adjustment.BinLocationId,
                MovementType = adjustment.Reason == AdjustmentReason.LossInTransit ? MovementType.WriteOff : MovementType.Adjustment,
                Quantity = Math.Abs(adjustment.QuantityChange),
                ReferenceType = ReferenceType.Adjustment,
                ReferenceId = adjustment.Id,
                PerformedBy = adjustment.PerformedBy,
                CreatedAt = DateTime.UtcNow
            };
            await _uow.Repository<StockMovement>().AddAsync(movement);
            
            await _notificationService.SendNotificationAsync(adjustment.PerformedBy, NotificationChannel.InApp, 
                "AdjustmentApproved", "Stock Adjustment Approved", 
                $"Your adjustment request {adjustment.AdjustmentNumber} has been approved by {approver.FullName}.", 
                "StockAdjustment", adjustment.Id);
        }
        else
        {
            adjustment.Status = AdjustmentStatus.Rejected;
            
            await _notificationService.SendNotificationAsync(adjustment.PerformedBy, NotificationChannel.InApp, 
                "AdjustmentRejected", "Stock Adjustment Rejected", 
                $"Your adjustment request {adjustment.AdjustmentNumber} has been rejected by {approver.FullName}.", 
                "StockAdjustment", adjustment.Id);
        }

        adjustment.ApprovedBy = secureApproverId;
        _uow.Repository<StockAdjustment>().Update(adjustment);
        
        try
        {
            await _uow.CommitAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict detected during stock adjustment approval for Adjustment {AdjustmentId}", adjustmentId);
            var currentStock = await _uow.Repository<StockLevel>().Query().AsNoTracking()
                .Where(sl => sl.ProductId == adjustment.ProductId && sl.WarehouseId == adjustment.WarehouseId && sl.BinLocationId == adjustment.BinLocationId)
                .Select(sl => (int?)sl.QuantityOnHand)
                .FirstOrDefaultAsync();
            throw new StaleDataException("StockLevel", currentStock);
        }

        if (adjustment.Reason == AdjustmentReason.LossInTransit
            && adjustment.ReferenceType == ReferenceType.Transfer
            && adjustment.ReferenceId.HasValue)
        {
            await _varianceResolver.TryResolveTransferVarianceAsync(adjustment.ReferenceId.Value);
        }

        return await MapAdjustmentToResponseDtoAsync(adjustment, product!, warehouse!, performer!, approver);
    }

    private async Task<StockAdjustmentResponseDto> MapAdjustmentToResponseDtoAsync(
        StockAdjustment adj,
        Product prod,
        Warehouse wh,
        User performer,
        User? approver)
    {
        string? transferNumber = null;
        if (adj.ReferenceType == ReferenceType.Transfer && adj.ReferenceId.HasValue)
        {
            transferNumber = await _uow.Repository<WarehouseTransfer>()
                .Query()
                .AsNoTracking()
                .Where(t => t.Id == adj.ReferenceId.Value)
                .Select(t => t.TransferNumber)
                .FirstOrDefaultAsync();
        }

        return new StockAdjustmentResponseDto
        {
            Id = adj.Id,
            AdjustmentNumber = adj.AdjustmentNumber,
            ProductId = adj.ProductId,
            ProductName = prod.Name,
            ProductSKU = prod.SKU,
            WarehouseId = adj.WarehouseId,
            WarehouseName = wh.Name,
            BinLocationId = adj.BinLocationId,
            Reason = adj.Reason,
            Status = adj.Status,
            QuantityBefore = adj.QuantityBefore,
            QuantityAfter = adj.QuantityAfter,
            QuantityChange = adj.QuantityChange,
            Notes = adj.Notes,
            PerformedBy = adj.PerformedBy,
            PerformedByUserName = performer.FullName,
            ApprovedBy = adj.ApprovedBy,
            ApprovedByUserName = approver?.FullName,
            CreatedAt = adj.CreatedAt,
            ReferenceType = adj.ReferenceType,
            ReferenceId = adj.ReferenceId,
            TransferNumber = transferNumber,
            ShrinkageReason = adj.ShrinkageReason
        };
    }

    private static StockAdjustmentResponseDto MapAdjustmentToResponseDto(
        StockAdjustment adj, Product prod, Warehouse wh, User performer, User? approver, string? transferNumber = null)
    {
        return new StockAdjustmentResponseDto
        {
            Id = adj.Id,
            AdjustmentNumber = adj.AdjustmentNumber,
            ProductId = adj.ProductId,
            ProductName = prod.Name,
            ProductSKU = prod.SKU,
            WarehouseId = adj.WarehouseId,
            WarehouseName = wh.Name,
            BinLocationId = adj.BinLocationId,
            Reason = adj.Reason,
            Status = adj.Status,
            QuantityBefore = adj.QuantityBefore,
            QuantityAfter = adj.QuantityAfter,
            QuantityChange = adj.QuantityChange,
            Notes = adj.Notes,
            PerformedBy = adj.PerformedBy,
            PerformedByUserName = performer.FullName,
            ApprovedBy = adj.ApprovedBy,
            ApprovedByUserName = approver?.FullName,
            CreatedAt = adj.CreatedAt,
            ReferenceType = adj.ReferenceType,
            ReferenceId = adj.ReferenceId,
            TransferNumber = transferNumber,
            ShrinkageReason = adj.ShrinkageReason
        };
    }

    private async Task<Dictionary<Guid, string>> ResolveTransferNumbersAsync(IEnumerable<StockAdjustment> adjustments)
    {
        var transferIds = adjustments
            .Where(a => a.ReferenceType == ReferenceType.Transfer && a.ReferenceId.HasValue)
            .Select(a => a.ReferenceId!.Value)
            .Distinct()
            .ToList();

        if (transferIds.Count == 0)
            return new Dictionary<Guid, string>();

        return await _uow.Repository<WarehouseTransfer>()
            .Query()
            .AsNoTracking()
            .Where(t => transferIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.TransferNumber);
    }

    public async Task<PagedResult<StockAdjustmentResponseDto>> GetAdjustmentsAsync(
        StockAdjustmentQueryParameters queryParams)
    {
        var query = _uow.Repository<StockAdjustment>()
            .Query()
            .Include(a => a.Product)
            .Include(a => a.Warehouse)
            .Include(a => a.PerformedByUser)
            .Include(a => a.ApprovedByUser)
            .AsQueryable();

        if (queryParams.ProductId.HasValue)
            query = query.Where(a => a.ProductId == queryParams.ProductId.Value);

        if (queryParams.WarehouseId.HasValue)
            query = query.Where(a => a.WarehouseId == queryParams.WarehouseId.Value);

        if (queryParams.Status.HasValue)
            query = query.Where(a => a.Status == queryParams.Status.Value);

        if (queryParams.Reason.HasValue)
            query = query.Where(a => a.Reason == queryParams.Reason.Value);

        if (queryParams.ReferenceType.HasValue)
            query = query.Where(a => a.ReferenceType == queryParams.ReferenceType.Value);

        if (queryParams.ReferenceId.HasValue)
            query = query.Where(a => a.ReferenceId == queryParams.ReferenceId.Value);

        if (!string.IsNullOrWhiteSpace(queryParams.Search))
            query = query.Where(a => a.AdjustmentNumber.Contains(queryParams.Search));

        int total = await query.CountAsync();

        var data = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .ToListAsync();

        var transferNumbers = await ResolveTransferNumbersAsync(data);

        return new PagedResult<StockAdjustmentResponseDto>
        {
            Data = data.Select(a => MapAdjustmentToResponseDto(
                a, a.Product, a.Warehouse, a.PerformedByUser, a.ApprovedByUser,
                a.ReferenceType == ReferenceType.Transfer && a.ReferenceId.HasValue
                    ? transferNumbers.GetValueOrDefault(a.ReferenceId.Value)
                    : null)),
            TotalCount = total,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }

    public async Task<StockAdjustmentResponseDto> GetAdjustmentByIdAsync(Guid adjustmentId)
    {
        var adj = await _uow.Repository<StockAdjustment>()
            .Query()
            .Include(a => a.Product)
            .Include(a => a.Warehouse)
            .Include(a => a.PerformedByUser)
            .Include(a => a.ApprovedByUser)
            .FirstOrDefaultAsync(a => a.Id == adjustmentId);

        if (adj == null) throw new NotFoundException("StockAdjustment", adjustmentId);

        var response = await MapAdjustmentToResponseDtoAsync(adj, adj.Product, adj.Warehouse, adj.PerformedByUser, adj.ApprovedByUser);
        await PopulateStaleStockDetailsAsync(response, adj);
        return response;
    }

    private async Task PopulateStaleStockDetailsAsync(StockAdjustmentResponseDto response, StockAdjustment adjustment)
    {
        var currentQuantity = await _uow.Repository<StockLevel>()
            .Query()
            .AsNoTracking()
            .Where(sl => sl.ProductId == adjustment.ProductId &&
                         sl.WarehouseId == adjustment.WarehouseId &&
                         sl.BinLocationId == adjustment.BinLocationId)
            .Select(sl => (int?)sl.QuantityOnHand)
            .FirstOrDefaultAsync() ?? 0;

        response.CurrentQuantity = currentQuantity;
        response.IsStale = currentQuantity != adjustment.QuantityBefore;
        response.WarningMessage = response.IsStale
            ? "Stock has changed since this adjustment was created."
            : null;
    }

    public async Task<bool> CancelStockAdjustmentAsync(Guid adjustmentId, Guid performedBy)
    {
        var adjustment = await _uow.Repository<StockAdjustment>()
            .Query()
            .FirstOrDefaultAsync(a => a.Id == adjustmentId);

        if (adjustment == null)
            throw new NotFoundException("StockAdjustment", adjustmentId);

        if (adjustment.Status != AdjustmentStatus.Approved)
            throw new BusinessRuleException("Only approved adjustments can be reversed.");

        // Revert physical stock
        var stock = await _uow.Repository<StockLevel>()
            .Query()
            .FirstOrDefaultAsync(sl => sl.ProductId == adjustment.ProductId &&
                                       sl.WarehouseId == adjustment.WarehouseId &&
                                       sl.BinLocationId == adjustment.BinLocationId);

        if (stock != null)
        {
            if (adjustment.Reason == AdjustmentReason.LossInTransit)
            {
                stock.QuantityInTransit += Math.Abs(adjustment.QuantityChange);
            }
            else
            {
                // Reverse the change.
                stock.QuantityOnHand -= adjustment.QuantityChange;
                if (stock.QuantityOnHand < 0) 
                    throw new InsufficientStockException(adjustment.ProductId.ToString(), adjustment.QuantityChange, stock.QuantityOnHand + adjustment.QuantityChange);
            }

            stock.LastUpdated = DateTime.UtcNow;
            _uow.Repository<StockLevel>().Update(stock);

            // Create Reversal Movement
            var movement = new StockMovement
            {
                Id = Guid.NewGuid(),
                ProductId = adjustment.ProductId,
                WarehouseId = adjustment.WarehouseId,
                BinLocationId = adjustment.BinLocationId,
                MovementType = MovementType.Adjustment,
                Quantity = Math.Abs(adjustment.QuantityChange),
                ReferenceType = ReferenceType.Adjustment,
                ReferenceId = adjustment.Id,
                PerformedBy = performedBy,
                CreatedAt = DateTime.UtcNow
            };
            await _uow.Repository<StockMovement>().AddAsync(movement);

            //Reverse Bin Spatial Capacity
            if (adjustment.BinLocationId.HasValue && adjustment.QuantityChange != 0)
            {
                var product = await _uow.Repository<Product>().GetByIdAsync(adjustment.ProductId);
                var binForCapacity = await _uow.Repository<BinLocation>()
                    .Query().Include(b => b.Zone)
                    .FirstOrDefaultAsync(b => b.Id == adjustment.BinLocationId.Value);

                if (product != null && binForCapacity != null)
                {
                    decimal volumeDelta = adjustment.QuantityChange * product.VolumeCm3;
                    decimal weightDelta = adjustment.QuantityChange * product.WeightKg;

                    // Reverse the original capacity change
                    binForCapacity.UtilizedVolumeCm3 = Math.Max(0, binForCapacity.UtilizedVolumeCm3 - volumeDelta);
                    binForCapacity.UtilizedWeightKg = Math.Max(0, binForCapacity.UtilizedWeightKg - weightDelta);
                    _uow.Repository<BinLocation>().Update(binForCapacity);
                }
            }
        }

        adjustment.Status = AdjustmentStatus.Cancelled;
        _uow.Repository<StockAdjustment>().Update(adjustment);

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
