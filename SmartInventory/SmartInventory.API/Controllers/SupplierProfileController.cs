using Asp.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Core.DTOs.SupplierPortal;
using SmartInventory.Core.Interfaces;
using System.Security.Claims;

namespace SmartInventory.API.Controllers;


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


    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var result = await _service.GetProfileAsync(GetSupplierId(), GetContactId());
        return Ok(result);
    }


    [EnableRateLimiting("mutations")]
    [HttpPut]
    public async Task<IActionResult> UpdateProfile([FromBody] SupplierUpdateProfileRequest request)
    {
        await _service.UpdateProfileAsync(GetSupplierId(), GetContactId(), request);
        return Ok(new { message = "Profile updated successfully." });
    }

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


    [HttpGet("status")]
    public async Task<IActionResult> GetOnboardingStatus()
    {
        var status = await _service.GetOnboardingStatusAsync(GetSupplierId());
        return Ok(status);
    }


    [EnableRateLimiting("mutations")]
    [HttpPost("submit-info")]
    public async Task<IActionResult> SubmitOnboardingInfo([FromBody] SupplierSubmitInfoRequest request)
    {
        await _service.SubmitOnboardingInfoAsync(GetSupplierId(), request);
        return Ok(new { message = "Information submitted successfully. Your profile is now pending review." });
    }

    [HttpGet("agreement")]
    public async Task<IActionResult> GetAgreement()
    {
        var text = await _service.GetAgreementAsync(GetSupplierId());
        return Ok(new { agreementText = text });
    }


    [EnableRateLimiting("mutations")]
    [HttpPost("agreement/accept")]
    public async Task<IActionResult> AcceptAgreement()
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await _service.AcceptAgreementAsync(GetSupplierId(), ipAddress);
        return Ok(new { message = "Agreement accepted successfully. Your account is now active!" });
    }
}
