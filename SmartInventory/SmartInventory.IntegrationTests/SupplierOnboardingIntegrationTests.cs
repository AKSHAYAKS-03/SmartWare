using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartInventory.Core.DTOs.SupplierPortal;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;
using SmartInventory.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Xunit;

namespace SmartInventory.IntegrationTests;

public class SupplierTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public SupplierTestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim>();

        if (Request.Headers.TryGetValue("X-Test-Role", out var roleVal))
        {
            claims.Add(new Claim(ClaimTypes.Role, roleVal.ToString()));
            claims.Add(new Claim("role", roleVal.ToString()));
        }
        else
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        if (Request.Headers.TryGetValue("X-Test-SupplierId", out var supplierIdVal))
        {
            claims.Add(new Claim("supplierId", supplierIdVal.ToString()));
        }

        if (Request.Headers.TryGetValue("X-Test-ContactId", out var contactIdVal))
        {
            claims.Add(new Claim("contactId", contactIdVal.ToString()));
            claims.Add(new Claim(ClaimTypes.NameIdentifier, contactIdVal.ToString()));
        }
        else
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, "b0d33b91-4567-4eef-b123-888888888801"));
        }

        claims.Add(new Claim(ClaimTypes.Name, "Test User"));

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        var result = AuthenticateResult.Success(ticket);
        return Task.FromResult(result);
    }
}

public class SupplierWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseInMemoryDatabase"] = "true"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "TestScheme";
                options.DefaultChallengeScheme = "TestScheme";
            })
            .AddScheme<AuthenticationSchemeOptions, SupplierTestAuthHandler>("TestScheme", options => { });

            var sp = services.BuildServiceProvider();
            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureCreated();
            }
        });
    }
}

public class SupplierOnboardingIntegrationTests : IClassFixture<SupplierWebApplicationFactory>
{
    private readonly SupplierWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SupplierOnboardingIntegrationTests(SupplierWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ChangeTracker.Clear();

            // Clear tables to prevent data collision across test runs
            db.SupplierRefreshTokens.RemoveRange(db.SupplierRefreshTokens);
            db.SupplierInvoices.RemoveRange(db.SupplierInvoices);
            db.SupplierContacts.RemoveRange(db.SupplierContacts);
            db.Suppliers.RemoveRange(db.Suppliers);
            db.SaveChanges();
        }
    }

    private void SetAuthHeaders(string role, Guid? supplierId = null, Guid? contactId = null)
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("X-Test-Role", role);
        if (supplierId.HasValue)
        {
            _client.DefaultRequestHeaders.Add("X-Test-SupplierId", supplierId.Value.ToString());
        }
        if (contactId.HasValue)
        {
            _client.DefaultRequestHeaders.Add("X-Test-ContactId", contactId.Value.ToString());
        }
    }

    [Fact]
    public async Task Complete_SelfRegistration_Approval_Agreement_Flow()
    {
        // 1. Self Register
        var regRequest = new SupplierRegisterRequest(
            Name: "Acme Components Ltd",
            GSTIN: "27AABCT1234C1Z5",
            PAN: "ABCDE1234F",
            Address: "123 Industrial Way",
            ContactFullName: "John Doe",
            Email: "john@acme.com",
            Phone: "+919876543210",
            Password: "Password@123"
        );

        var regResponse = await _client.PostAsJsonAsync("/api/v1/supplier/auth/register", regRequest);
        Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);

        // 2. Fetch token from database (since email dispatch is mock/simulated)
        string verifyToken;
        Guid supplierId;
        Guid contactId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var contact = await db.SupplierContacts.Include(c => c.Supplier)
                .FirstAsync(c => c.Email == "john@acme.com");
            verifyToken = contact.EmailVerifyToken!;
            supplierId = contact.SupplierId;
            contactId = contact.Id;
            Assert.False(contact.EmailVerified);
            Assert.Equal(SupplierStatus.Registered, contact.Supplier.Status);
        }

        // 3. Verify Email
        var verifyRequest = new SupplierVerifyEmailRequest("john@acme.com", verifyToken);
        var verifyResponse = await _client.PostAsJsonAsync("/api/v1/supplier/auth/verify-email", verifyRequest);
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var contact = await db.SupplierContacts.Include(c => c.Supplier).FirstAsync(c => c.Id == contactId);
            Assert.True(contact.EmailVerified);
            Assert.Equal(SupplierStatus.PendingReview, contact.Supplier.Status);
        }

        // 4. Request Info as Admin
        SetAuthHeaders("Admin");
        var infoRequest = new SupplierReviewRequest("RequestMoreInfo", "Please submit your tax certificate.", null, null, null);
        var infoResponse = await _client.PostAsJsonAsync($"/api/v1/suppliers/{supplierId}/review", infoRequest);
        Assert.Equal(HttpStatusCode.OK, infoResponse.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var supplier = await db.Suppliers.FindAsync(supplierId);
            Assert.Equal(SupplierStatus.InfoRequested, supplier!.Status);
            Assert.Equal("Please submit your tax certificate.", supplier.InfoRequestedMessage);
        }

        // 5. Submit Info as Supplier
        SetAuthHeaders("Supplier", supplierId, contactId);
        var submitInfo = new SupplierSubmitInfoRequest("Tax Certificate attached (simulated)");
        var submitResponse = await _client.PostAsJsonAsync("/api/v1/supplier/profile/submit-info", submitInfo);
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var supplier = await db.Suppliers.FindAsync(supplierId);
            Assert.Equal(SupplierStatus.PendingReview, supplier!.Status);
            Assert.Null(supplier.InfoRequestedMessage);
        }

        // 6. Approve as Admin
        SetAuthHeaders("Admin");
        var approveRequest = new SupplierReviewRequest("Approve", null, "ACM-001", 10000m, PaymentTerms.Net60);
        var approveResponse = await _client.PostAsJsonAsync($"/api/v1/suppliers/{supplierId}/review", approveRequest);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var supplier = await db.Suppliers.FindAsync(supplierId);
            Assert.Equal(SupplierStatus.AgreementPending, supplier!.Status);
            Assert.Equal("ACM-001", supplier.Code);
            Assert.Equal(10000m, supplier.CreditLimit);
            Assert.Equal(PaymentTerms.Net60, supplier.PaymentTerms);
        }

        // 7. Get Agreement & Accept as Supplier
        SetAuthHeaders("Supplier", supplierId, contactId);
        
        var agreementResponse = await _client.GetAsync("/api/v1/supplier/profile/agreement");
        Assert.Equal(HttpStatusCode.OK, agreementResponse.StatusCode);
        var agreementContent = await agreementResponse.Content.ReadAsStringAsync();
        Assert.Contains("ACM-001", agreementContent);

        var acceptResponse = await _client.PostAsync("/api/v1/supplier/profile/agreement/accept", null);
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var supplier = await db.Suppliers.FindAsync(supplierId);
            Assert.Equal(SupplierStatus.Active, supplier!.Status);
            Assert.NotNull(supplier.AgreementSignedAt);
        }

        // 8. Access transactional endpoints now allowed
        var poResponse = await _client.GetAsync("/api/v1/supplier/purchase-orders");
        Assert.Equal(HttpStatusCode.OK, poResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_Invite_And_CompleteRegistration_Flow()
    {
        // 1. Admin Invite
        SetAuthHeaders("Admin");
        var inviteRequest = new SupplierInviteRequest(
            Name: "Apex Logistics",
            GSTIN: "27AABCM1234C1Z5",
            Email: "onboard@apex.com",
            Phone: "+919876543211"
        );

        var inviteResponse = await _client.PostAsJsonAsync("/api/v1/suppliers/invite", inviteRequest);
        Assert.Equal(HttpStatusCode.OK, inviteResponse.StatusCode);

        // 2. Look up invite token
        string inviteToken;
        Guid supplierId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var supplier = await db.Suppliers.FirstAsync(s => s.Email == "onboard@apex.com");
            inviteToken = supplier.InviteToken!;
            supplierId = supplier.Id;
            Assert.Equal(SupplierStatus.InviteSent, supplier.Status);
        }

        // 3. Complete registration (Anonymous public route)
        _client.DefaultRequestHeaders.Clear();
        var completeRequest = new SupplierCompleteRegistrationRequest(
            InviteToken: inviteToken,
            ContactFullName: "Sarah Connor",
            JobTitle: "Logistics Director",
            PAN: "FGHIJ5678K",
            Address: "45 Logistical Lane",
            Password: "SecurePassword@321"
        );

        var completeResponse = await _client.PostAsJsonAsync("/api/v1/supplier/auth/complete-registration", completeRequest);
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var supplier = await db.Suppliers.Include(s => s.Contacts).FirstAsync(s => s.Id == supplierId);
            Assert.Equal(SupplierStatus.PendingReview, supplier.Status);
            Assert.Null(supplier.InviteToken);
            var contact = supplier.Contacts.First();
            Assert.Equal("Sarah Connor", contact.FullName);
            Assert.True(contact.EmailVerified); // Invitation path skips verification
        }
    }

    [Fact]
    public async Task LockedPortalMiddleware_Blocks_NonActive_Suppliers()
    {
        // 1. Create a non-Active supplier (PendingReview)
        Guid supplierId = Guid.NewGuid();
        Guid contactId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var supplier = new Supplier
            {
                Id = supplierId,
                Name = "Locked Supply Co",
                Code = "LSC",
                Email = "contact@locked.com",
                Address = "Locked St",
                IsActive = true,
                Status = SupplierStatus.PendingReview,
                CreatedAt = DateTime.UtcNow
            };
            var contact = new SupplierContact
            {
                Id = contactId,
                SupplierId = supplierId,
                FullName = "Locked Contact",
                Email = "contact@locked.com",
                PasswordHash = "dummy",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Suppliers.Add(supplier);
            db.SupplierContacts.Add(contact);
            await db.SaveChangesAsync();
        }

        // 2. Attempt to access transactional PO page as this supplier contact
        SetAuthHeaders("Supplier", supplierId, contactId);
        var response = await _client.GetAsync("/api/v1/supplier/purchase-orders");

        // Should return 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var errorObj = await response.Content.ReadFromJsonAsync<ErrorDetail>();
        Assert.NotNull(errorObj);
        Assert.Equal("Onboarding Incomplete", errorObj.Title);
        Assert.Equal("PendingReview", errorObj.SupplierStatus);
    }

    private class ErrorDetail
    {
        public string Title { get; set; } = string.Empty;
        public string SupplierStatus { get; set; } = string.Empty;
    }
}
