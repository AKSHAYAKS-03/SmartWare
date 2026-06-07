using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class TransfersController : ControllerBase
{
    private readonly ITransferService _transferService;
    private readonly ICurrentUserService _currentUser;

    public TransfersController(ITransferService transferService, ICurrentUserService currentUser)
    {
        _transferService = transferService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetTransfers([FromQuery] TransferQueryParameters queryParams)
    {
        if (_currentUser.WarehouseId.HasValue)
        {
            queryParams.WarehouseId = _currentUser.WarehouseId.Value;
        }
        return Ok(await _transferService.GetTransfersAsync(queryParams));
    }

    [HttpPost("search")]
    [EnableRateLimiting("reports")]
    public async Task<IActionResult> SearchTransfers([FromBody] DynamicQueryRequest request)
    {
        if (_currentUser.WarehouseId.HasValue)
        {
            // For transfers, scoping means they can view it if it either comes FROM or goes TO their warehouse.
            // A simple equal filter might restrict too much if not handled properly at the repository, 
            // but we'll apply a FromWarehouseId filter for safety, or rely on service-level logic.
            // For now, adding a strict FromWarehouseId filter to match the DTO approach.
            request.Filters.Add(new FilterCriteria
            {
                Field = "FromWarehouseId",
                Operator = "eq",
                Value = _currentUser.WarehouseId.Value.ToString()
            });
        }
        return Ok(await _transferService.SearchTransfersAsync(request));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTransfer(Guid id)
    {
        var result = await _transferService.GetTransferByIdAsync(id, _currentUser.WarehouseId);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "RequireStaff")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> CreateTransfer([FromBody] TransferCreateDto dto)
    {
        dto.RequestedBy = _currentUser.UserId;
        var result = await _transferService.CreateTransferAsync(dto);
        return CreatedAtAction(nameof(GetTransfer), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}/approve")]
    [Authorize(Policy = "RequireManager")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> ApproveTransfer(Guid id, [FromBody] TransferApprovalDto dto)
    {
        dto.ApprovedBy = _currentUser.UserId;
        var result = await _transferService.ApproveTransferAsync(id, dto);
        return Ok(result);
    }

    [HttpPut("{id:guid}/dispatch")]
    [Authorize(Policy = "RequireStaff")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> DispatchTransfer(Guid id)
    {
        var result = await _transferService.DispatchTransferAsync(id, _currentUser.UserId);
        return Ok(result);
    }

    [HttpPut("{id:guid}/receive")]
    [Authorize(Policy = "RequireStaff")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> ReceiveTransfer(Guid id, [FromBody] TransferReceiveDto dto)
    {
        var result = await _transferService.ReceiveTransferAsync(id, dto, _currentUser.UserId);
        return Ok(result);
    }

    [HttpPost("bin-to-bin")]
    [Authorize(Policy = "RequireStaff")]
    [EnableRateLimiting("mutations")]
    public async Task<IActionResult> TransferBinToBin([FromBody] BinTransferCreateDto dto)
    {
        // Enforce warehouse scope if applicable
        if (_currentUser.WarehouseId.HasValue && dto.WarehouseId != _currentUser.WarehouseId.Value)
        {
            return StatusCode(StatusCodes.Status403Forbidden, "You can only perform bin transfers in your assigned warehouse.");
        }

        var result = await _transferService.TransferBinToBinAsync(dto, _currentUser.UserId);
        return Ok(new { success = result, message = "Bin transfer completed successfully." });
    }
}
