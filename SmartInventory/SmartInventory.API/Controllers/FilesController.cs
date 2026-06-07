using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Interfaces;
using Asp.Versioning;

namespace SmartInventory.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/[controller]")]
[Authorize]
[EnableRateLimiting("mutations")]
public class FilesController : ControllerBase
{
    private readonly IFileAttachmentService _fileService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<FilesController> _logger;
    public FilesController(IFileAttachmentService fileService, ICurrentUserService currentUser, ILogger<FilesController> logger)
    {
        _fileService = fileService;
        _currentUser = currentUser;
        _logger = logger;
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadFile([FromForm] SmartInventory.API.DTOs.FileUploadRequest request)
    {
        _logger.LogInformation("UploadFile entered: EntityType={EntityType}, EntityId={EntityId}, Category={Category}, ExpiryDate={ExpiryDate}, FilePresent={FilePresent}",
            request.EntityType,
            request.EntityId,
            request.Category,
            request.ExpiryDate?.ToString("o"),
            request.File != null);
        // Log ModelState validity (auto‑validation from [ApiController])
        if (!ModelState.IsValid)
        {
            var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            _logger.LogWarning("ModelState invalid: {Errors}", errors);
        }
        if (request.File == null || request.File.Length == 0)
            return BadRequest("No file provided.");

        using var stream = request.File.OpenReadStream();
        var result = await _fileService.UploadFileAsync(
            stream, 
            request.File.FileName, 
            request.File.ContentType, 
            request.File.Length, 
            request.EntityType, 
            request.EntityId, 
            request.Category, 
            request.ExpiryDate, 
            _currentUser.UserId);

        return Ok(result);
    }

    [HttpGet("{entityType}/{entityId:guid}")]
    public async Task<IActionResult> GetFiles(string entityType, Guid entityId)
    {
        var files = await _fileService.GetFilesByEntityAsync(entityType, entityId);
        return Ok(files);
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> DownloadFile(Guid id)
    {
        var fileInfo = await _fileService.GetFileByIdAsync(id);
        
        var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "";
        if (fileInfo.UploadedBy != _currentUser.UserId && userRole != "Admin" && userRole != "Manager")
        {
            return Forbid();
        }

        var stream = await _fileService.GetFileStreamAsync(id);
        
        return File(stream, fileInfo.MimeType, fileInfo.FileName);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireManager")]
    public async Task<IActionResult> DeleteFile(Guid id)
    {
        await _fileService.DeleteFileAsync(id, _currentUser.UserId);
        return NoContent();
    }

    [HttpPut("{id:guid}/verify")]
    [Authorize(Policy = "RequireManager")]
    public async Task<IActionResult> VerifyFile(Guid id)
    {
        var result = await _fileService.VerifyFileAsync(id, _currentUser.UserId);
        return Ok(new { success = result, message = "File successfully verified." });
    }
}
