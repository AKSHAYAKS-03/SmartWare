using Asp.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Core.DTOs.SupplierPortal;
using SmartInventory.Core.Interfaces;
using System.Security.Claims;

namespace SmartInventory.API.Controllers;

/// <summary>
/// Supplier portal profile management endpoints.
/// Route prefix: /api/supplier/profile
///
/// GET    /api/supplier/profile       — View profile [Supplier]
/// PUT    /api/supplier/profile       — Update contact details [Supplier]
/// POST   /api/supplier/profile/logo  — Upload logo [Supplier]
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/supplier/profile")]
[Authorize(Policy = "RequireSupplier")]
public class SupplierProfileController : ControllerBase
{
    private readonly ISupplierProfileService _service;

    public SupplierProfileController(ISupplierProfileService service)
    {
        _service = service;
    }

    private Guid GetSupplierId() => Guid.Parse(User.FindFirstValue("supplierId")!);
    private Guid GetContactId() => Guid.Parse(User.FindFirstValue("contactId")!);

    /// <summary>
    /// Returns the supplier's profile including contact person details.
    /// Shows only the authenticated supplier's own data.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var result = await _service.GetProfileAsync(GetSupplierId(), GetContactId());
        return Ok(result);
    }

    /// <summary>
    /// Updates the contact person's name, phone number, and job title.
    /// Does not allow editing the supplier's core details (name, code, payment terms).
    /// </summary>
    [EnableRateLimiting("mutations")]
    [HttpPut]
    public async Task<IActionResult> UpdateProfile([FromBody] SupplierUpdateProfileRequest request)
    {
        await _service.UpdateProfileAsync(GetSupplierId(), GetContactId(), request);
        return Ok(new { message = "Profile updated successfully." });
    }

    /// <summary>
    /// Uploads a new logo image for the supplier.
    /// Accepts common image formats (JPEG, PNG, WebP).
    /// </summary>
    [EnableRateLimiting("mutations")]
    [HttpPost("logo")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadLogo(IFormFile logo)
    {
        var request = new SupplierUploadLogoRequest(
            FileStream: logo.OpenReadStream(),
            FileName: logo.FileName,
            ContentType: logo.ContentType
        );
        var logoPath = await _service.UploadLogoAsync(GetSupplierId(), request);
        return Ok(new { message = "Logo uploaded successfully.", path = logoPath });
    }

    /// <summary>
    /// Returns the current onboarding status, verification details, and request/rejection messages.
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetOnboardingStatus()
    {
        var status = await _service.GetOnboardingStatusAsync(GetSupplierId());
        return Ok(status);
    }

    /// <summary>
    /// Submits updated/requested profile details, transitioning the status back to PendingReview.
    /// </summary>
    [EnableRateLimiting("mutations")]
    [HttpPost("submit-info")]
    public async Task<IActionResult> SubmitOnboardingInfo([FromBody] SupplierSubmitInfoRequest request)
    {
        await _service.SubmitOnboardingInfoAsync(GetSupplierId(), request);
        return Ok(new { message = "Information submitted successfully. Your profile is now pending review." });
    }

    /// <summary>
    /// Retrieves the text of the onboarding partnership agreement.
    /// </summary>
    [HttpGet("agreement")]
    public async Task<IActionResult> GetAgreement()
    {
        var text = await _service.GetAgreementAsync(GetSupplierId());
        return Ok(new { agreementText = text });
    }

    /// <summary>
    /// Accepts the agreement and transitions the supplier to Active.
    /// </summary>
    [EnableRateLimiting("mutations")]
    [HttpPost("agreement/accept")]
    public async Task<IActionResult> AcceptAgreement()
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await _service.AcceptAgreementAsync(GetSupplierId(), ipAddress);
        return Ok(new { message = "Agreement accepted successfully. Your account is now active!" });
    }
}
