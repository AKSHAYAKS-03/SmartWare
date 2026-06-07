using SmartInventory.Core.DTOs.SupplierPortal;

namespace SmartInventory.Core.Interfaces;

/// <summary>
/// Profile management for a supplier contact in the portal.
/// Suppliers can update their own contact info and upload a logo.
/// </summary>
public interface ISupplierProfileService
{
    /// <summary>Returns the profile of the supplier associated with the given contact.</summary>
    Task<SupplierProfileDto> GetProfileAsync(Guid supplierId, Guid contactId);

    /// <summary>Updates the contact person's name, phone and job title for the portal account.</summary>
    Task UpdateProfileAsync(Guid supplierId, Guid contactId, SupplierUpdateProfileRequest request);

    /// <summary>Updates the supplier's logo by uploading a new image file.</summary>
    Task<string> UploadLogoAsync(Guid supplierId, SupplierUploadLogoRequest request);

    /// <summary>Retrieves current onboarding status, rejection/suspension reasons and request message.</summary>
    Task<SupplierOnboardingStatusResponse> GetOnboardingStatusAsync(Guid supplierId);

    /// <summary>Supplier submits updated/requested profile details, returning status to PendingReview.</summary>
    Task SubmitOnboardingInfoAsync(Guid supplierId, SupplierSubmitInfoRequest request);

    /// <summary>Retrieves the onboarding agreement text.</summary>
    Task<string> GetAgreementAsync(Guid supplierId);

    /// <summary>Accepts the agreement and moves status to Active, logging the timestamp and IP.</summary>
    Task AcceptAgreementAsync(Guid supplierId, string ipAddress);
}
