using System.Threading.RateLimiting;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using SmartInventory.Core;
using SmartInventory.Core.Interfaces;
using SmartInventory.Repository;
using SmartInventory.Repository.Repositories;
using SmartInventory.Repository.Services;
using SmartInventory.Service.Services;
using SmartInventory.Infrastructure.Services;
using SmartInventory.Infrastructure.BackgroundJobs;
using FluentValidation;
using FluentValidation.AspNetCore;


using Serilog;
using Asp.Versioning;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

// ─────────────────────────────────────────────────────────────────────────────
// CORS — Allow Angular dev server and production origins
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("SmartInventoryPolicy", policy =>
    {
        policy.WithOrigins(
                "http://localhost:4200",   // Angular dev server
                "http://localhost:3000",   // Alt dev port
                "https://smartinventory.app" // Production (update as needed)
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR WebSocket handshake
    });
});

// ─────────────────────────────────────────────────────────────────────────────
// RATE LIMITING — Protect auth endpoints from brute-force
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    // "auth" policy: max 5 requests per 60 seconds per IP on sign-in
    options.AddFixedWindowLimiter("auth", o =>
    {
        o.Window = TimeSpan.FromMinutes(1);
        o.PermitLimit = 5;
        o.QueueLimit = 0;
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
    
    // "reports" policy: max 10 requests per 60 seconds per IP
    options.AddFixedWindowLimiter("reports", o =>
    {
        o.Window = TimeSpan.FromMinutes(1);
        o.PermitLimit = 10;
        o.QueueLimit = 2; // Allow small queueing for concurrent dashboards
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // "mutations" policy: max 30 requests per 60 seconds per IP
    options.AddFixedWindowLimiter("mutations", o =>
    {
        o.Window = TimeSpan.FromMinutes(1);
        o.PermitLimit = 30;
        o.QueueLimit = 5;
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // Global fallback policy (Applies to all other routes)
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        // Limit by IP address
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        
        return RateLimitPartition.GetFixedWindowLimiter(ipAddress, _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = 100, // Maximum 100 requests per minute
            QueueLimit = 10,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });
    
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ─────────────────────────────────────────────────────────────────────────────
// CONTROLLERS + SWAGGER
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();

// Register FluentValidation auto-validation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<SmartInventory.Core.Validators.LoginValidator>();

// ─────────────────────────────────────────────────────────────────────────────
// API VERSIONING
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = false; // Enterprise strict: version must be explicit
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("x-api-version"),
        new QueryStringApiVersionReader("api-version"));
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddEndpointsApiExplorer();

// JWT Settings from appsettings.json
builder.Services.Configure<JWTsettings>(builder.Configuration.GetSection("JWTSettings"));

// ─────────────────────────────────────────────────────────────────────────────
// DATABASE
// ─────────────────────────────────────────────────────────────────────────────
if (builder.Configuration["UseInMemoryDatabase"] == "true")
{
    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        options.UseInMemoryDatabase("IntegrationTestsDb");
    });
}
else
{
    builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    {
        options.UseNpgsql(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            });
    });
}

// ─────────────────────────────────────────────────────────────────────────────
// ─────────────────────────────────────────────────────────────────────────────
// MEMORY CACHE & CACHE SERVICE (Fallback) & REDIS CACHE
// ─────────────────────────────────────────────────────────────────────────────
var redisConnectionString = builder.Configuration.GetConnectionString("RedisConnection");
if (!string.IsNullOrEmpty(redisConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "SmartWare_";
    });
    builder.Services.AddSingleton<ICacheService, SmartInventory.Infrastructure.Services.RedisCacheService>();
    
    // Register multiplexer for pub/sub
    builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp => 
        StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnectionString));
}
else
{
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<ICacheService, SmartInventory.Infrastructure.Services.MemoryCacheService>();
}

// ─────────────────────────────────────────────────────────────────────────────
// HTTP CONTEXT ACCESSOR + CURRENT USER SERVICE
// Required for ICurrentUserService to read JWT claims per request
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ─────────────────────────────────────────────────────────────────────────────
// HOSTED SERVICES
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddHostedService<SmartInventory.Infrastructure.Services.OutboxProcessorService>();

// ─────────────────────────────────────────────────────────────────────────────
// REPOSITORY LAYER
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
builder.Services.AddScoped<ITransferRepository, TransferRepository>();
builder.Services.AddScoped<IBarcodeRepository, BarcodeRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IStockLevelRepository, StockLevelRepository>();
builder.Services.AddScoped<ISequenceNumberGenerator, SequenceNumberGenerator>();
builder.Services.AddScoped(typeof(IUnitOfWork), typeof(UnitOfWork));

// ─────────────────────────────────────────────────────────────────────────────
// REALTIME (SignalR)
// ─────────────────────────────────────────────────────────────────────────────
var signalRBuilder = builder.Services.AddSignalR();
if (!string.IsNullOrEmpty(redisConnectionString))
{
    signalRBuilder.AddStackExchangeRedis(redisConnectionString);
}

// ─────────────────────────────────────────────────────────────────────────────
// MEDIATR & SERVICE LAYER
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<SmartInventory.Service.Handlers.PurchaseOrderNotificationHandler>());

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IFileValidationService, FileValidationService>();
builder.Services.AddScoped<IBarcodeService, BarcodeService>();
builder.Services.AddScoped<IRealtimeService, RealtimeService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IStockLevelService, StockLevelService>();
builder.Services.AddScoped<IStockAdjustmentService, StockAdjustmentService>();
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
builder.Services.AddScoped<ITransferVarianceResolver, TransferVarianceResolver>();
builder.Services.AddScoped<ITransferService, TransferService>();
// Phase 2 — new services
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<ITokenBlacklistService, TokenBlacklistService>();
builder.Services.AddScoped<IInvoiceProcessingService, InvoiceProcessingService>();
builder.Services.AddScoped<IFileAttachmentService, FileAttachmentService>();
builder.Services.AddScoped<IMasterDataService, MasterDataService>();
builder.Services.AddScoped<IInventoryValuationService, InventoryValuationService>();
builder.Services.AddScoped<IWarehouseService, WarehouseService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IEncryptionService, EncryptionService>();
// ─────────────────────────────────────────────────────────────────────────────
// SUPPLIER PORTAL SERVICES — Isolated from internal user auth + data
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<ISupplierAuthService, SupplierAuthService>();
builder.Services.AddScoped<ISupplierPurchaseOrderService, SupplierPurchaseOrderService>();
builder.Services.AddScoped<ISupplierInvoiceService, SupplierInvoiceService>();
builder.Services.AddScoped<ISupplierCatalogueService, SupplierCatalogueService>();
builder.Services.AddScoped<ISupplierDashboardService, SupplierDashboardService>();
builder.Services.AddScoped<ISupplierProfileService, SupplierProfileService>();

// ─────────────────────────────────────────────────────────────────────────────
// BACKGROUND JOBS
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddHostedService<AuditLogArchiveJob>();
builder.Services.AddHostedService<POOverdueCheckerJob>();

// ─────────────────────────────────────────────────────────────────────────────
// EMAIL
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IEmailService, SmtpEmailService>();

// ─────────────────────────────────────────────────────────────────────────────
// HEALTH CHECKS
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddCheck<SmartInventory.API.Services.DatabaseHealthCheck>("database_check");

// ─────────────────────────────────────────────────────────────────────────────
// JWT AUTHENTICATION
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var secretKey = builder.Configuration["JWTSettings:SecretKey"] ?? string.Empty;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JWTSettings:Issuer"],
            ValidAudience = builder.Configuration["JWTSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero // No grace period — tokens expire exactly on schedule
        };

        // Allow SignalR to pass token via query string (WebSocket handshake cannot use headers)
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var blacklistService = context.HttpContext.RequestServices.GetRequiredService<ITokenBlacklistService>();
                var userIdString = context.Principal?.FindFirstValue("userId");
                
                if (Guid.TryParse(userIdString, out var userId))
                {
                    var issuedAt = context.SecurityToken.ValidFrom;
                    if (await blacklistService.IsUserBlacklistedAsync(userId, issuedAt))
                    {
                        context.Fail("Token has been revoked due to security reasons.");
                    }
                }
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    // True Permissions-Based Authorization Policies
    options.AddPolicy("RequireAdmin", policy => policy.RequireClaim("Permission", "Admin"));
    options.AddPolicy("RequireManager", policy => policy.RequireClaim("Permission", "Manage"));
    options.AddPolicy("RequireStaff", policy => policy.RequireClaim("Permission", "Inventory"));
    options.AddPolicy("RequireViewer", policy => policy.RequireClaim("Permission", "View"));
    options.AddPolicy("RequireSupplier", policy => policy.RequireClaim("Permission", "Supplier"));
    
    // Enterprise Capacity Controls
    options.AddPolicy("CanOverrideCapacity", policy => policy.RequireRole("Admin", "Manager"));
});

// ─────────────────────────────────────────────────────────────────────────────
// SWAGGER WITH JWT BEARER SUPPORT
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "SmartInventory WMS API", Version = "v1" });
    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization. Enter: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", null, null),
            new List<string>()
        }
    });
});

// ─────────────────────────────────────────────────────────────────────────────
// BUILD + MIDDLEWARE PIPELINE
// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ★★ Request logging middleware (helps debug 400 before controller) ★★
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Incoming request: {Method} {Path}", context.Request.Method, context.Request.Path);
    await next();
});

app.UseMiddleware<SmartInventory.API.Middleware.CorrelationIdMiddleware>();
app.UseMiddleware<SmartInventory.API.Middleware.ExceptionMiddleware>();

app.UseCors("SmartInventoryPolicy");
app.UseRateLimiter();
// app.UseHttpsRedirection(); // Disabled: server only binds HTTP on :5245 in dev.
// Re-enable this when an HTTPS listener is configured in launchSettings.json.
app.UseAuthentication();
app.UseMiddleware<SmartInventory.API.Middleware.LogContextEnrichmentMiddleware>();

// Supplier portal isolation: blocks Supplier-role tokens from accessing internal API routes
app.UseMiddleware<SmartInventory.API.Middleware.SupplierAuthorizationMiddleware>();

app.UseAuthorization();

app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => true
});

app.MapControllers();
app.MapHub<SmartInventory.Infrastructure.Hubs.NotificationHub>("/hubs/notifications");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartInventory WMS API v1"));
}

app.Run();

public partial class Program { }
