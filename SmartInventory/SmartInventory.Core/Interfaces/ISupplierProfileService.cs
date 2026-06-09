using SmartInventory.Core.DTOs.SupplierPortal;

namespace SmartInventory.Core.Interfaces;
public interface ISupplierProfileService
{
    Task<SupplierProfileDto> GetProfileAsync(Guid supplierId, Guid contactId);

    Task UpdateProfileAsync(Guid supplierId, Guid contactId, SupplierUpdateProfileRequest request);

    Task<string> UploadLogoAsync(Guid supplierId, SupplierUploadLogoRequest request);

    Task<SupplierOnboardingStatusResponse> GetOnboardingStatusAsync(Guid supplierId);

    Task SubmitOnboardingInfoAsync(Guid supplierId, SupplierSubmitInfoRequest request);

    Task<string> GetAgreementAsync(Guid supplierId);

    Task AcceptAgreementAsync(Guid supplierId, string ipAddress);
}
