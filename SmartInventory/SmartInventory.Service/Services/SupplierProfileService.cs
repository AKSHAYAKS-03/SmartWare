using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.DTOs.SupplierPortal;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Exceptions;
using SmartInventory.Core.Interfaces;
using SmartInventory.Core.Enums;

namespace SmartInventory.Service.Services;

public class SupplierProfileService : ISupplierProfileService
{
    private readonly IUnitOfWork _uow;
    private readonly IFileStorageService _fileStorage;

    public SupplierProfileService(IUnitOfWork uow, IFileStorageService fileStorage)
    {
        _uow = uow;
        _fileStorage = fileStorage;
    }





    public async Task<SupplierProfileDto> GetProfileAsync(Guid supplierId, Guid contactId)
    {
        var contact = await _uow.Repository<SupplierContact>().Query()
            .Include(c => c.Supplier)
            .FirstOrDefaultAsync(c => c.Id == contactId && c.SupplierId == supplierId && c.IsActive);

        if (contact == null)
            throw new NotFoundException("SupplierContact", contactId);

        var supplier = contact.Supplier;

        return new SupplierProfileDto(
            SupplierId: supplier.Id,
            Name: supplier.Name,
            Code: supplier.Code,
            Address: supplier.Address,
            ContactPersonName: contact.FullName,
            ContactEmail: contact.Email,
            ContactPhone: contact.Phone,
            JobTitle: contact.JobTitle,
            LeadTimeDays: supplier.LeadTimeDays,
            Rating: supplier.Rating,
            IsActive: supplier.IsActive
        );
    }





    public async Task UpdateProfileAsync(Guid supplierId, Guid contactId, SupplierUpdateProfileRequest request)
    {
        var contact = await _uow.Repository<SupplierContact>().Query()
            .FirstOrDefaultAsync(c => c.Id == contactId && c.SupplierId == supplierId && c.IsActive);

        if (contact == null)
            throw new NotFoundException("SupplierContact", contactId);

        contact.FullName = request.FullName;
        contact.Phone = request.Phone;
        contact.JobTitle = request.JobTitle;

        _uow.Repository<SupplierContact>().Update(contact);
        await _uow.CommitAsync();
    }





    public async Task<string> UploadLogoAsync(Guid supplierId, SupplierUploadLogoRequest request)
    {
    
        var supplier = await _uow.Repository<Supplier>().Query()
            .FirstOrDefaultAsync(s => s.Id == supplierId && s.IsActive);

        if (supplier == null)
            throw new NotFoundException("Supplier", supplierId);

    
        var logoPath = await _fileStorage.SaveFileAsync(
            request.FileStream,
            request.FileName,
            "supplier-logos",
            $"LOGO_{supplier.Code}"
        );

        return logoPath;
    }




    public async Task<SupplierOnboardingStatusResponse> GetOnboardingStatusAsync(Guid supplierId)
    {
        var supplier = await _uow.Repository<Supplier>().Query()
            .Include(s => s.Contacts)
            .FirstOrDefaultAsync(s => s.Id == supplierId);

        if (supplier == null)
            throw new NotFoundException("Supplier", supplierId);

        var emailVerified = supplier.Contacts.Any(c => c.EmailVerified);

        return new SupplierOnboardingStatusResponse(
            Status: supplier.Status,
            StatusName: supplier.Status.ToString(),
            RejectionReason: supplier.RejectionReason,
            SuspensionReason: supplier.SuspensionReason,
            InfoRequestedMessage: supplier.InfoRequestedMessage,
            EmailVerified: emailVerified
        );
    }




    public async Task SubmitOnboardingInfoAsync(Guid supplierId, SupplierSubmitInfoRequest request)
    {
        var supplier = await _uow.Repository<Supplier>().Query()
            .FirstOrDefaultAsync(s => s.Id == supplierId && s.IsActive);

        if (supplier == null)
            throw new NotFoundException("Supplier", supplierId);

        if (supplier.Status != SupplierStatus.InfoRequested)
            throw new BusinessRuleException("You can only submit additional information when requested by the administrator.");

        supplier.Status = SupplierStatus.PendingReview;
        supplier.InfoRequestedMessage = null;

        _uow.Repository<Supplier>().Update(supplier);
        await _uow.CommitAsync();
    }




    public async Task<string> GetAgreementAsync(Guid supplierId)
    {
        var supplier = await _uow.Repository<Supplier>().Query()
            .FirstOrDefaultAsync(s => s.Id == supplierId && s.IsActive);

        if (supplier == null)
            throw new NotFoundException("Supplier", supplierId);

        if (supplier.Status != SupplierStatus.AgreementPending && supplier.Status != SupplierStatus.Active)
            throw new BusinessRuleException("Agreement is not pending or active for this supplier.");

        return $"Standard Partnership Agreement for {supplier.Name} ({supplier.Code}).\n" +
               "By clicking accept, you agree to supply materials in accordance with our Smart Inventory guidelines,\n" +
               $"subject to payment terms of {supplier.PaymentTerms} and a credit limit of ${supplier.CreditLimit:N2}.\n" +
               "This agreement is legally binding and governs all purchase orders and invoices handled through this portal.";
    }




    public async Task AcceptAgreementAsync(Guid supplierId, string ipAddress)
    {
        var supplier = await _uow.Repository<Supplier>().Query()
            .FirstOrDefaultAsync(s => s.Id == supplierId && s.IsActive);

        if (supplier == null)
            throw new NotFoundException("Supplier", supplierId);

        if (supplier.Status != SupplierStatus.AgreementPending)
            throw new BusinessRuleException("Agreement is not pending acceptance.");

        supplier.Status = SupplierStatus.Active;
        supplier.AgreementSignedAt = DateTime.UtcNow;
        supplier.AgreementSignedIp = ipAddress;

        _uow.Repository<Supplier>().Update(supplier);
        await _uow.CommitAsync();
    }
}
