# SMARTWARE: LAYER-BY-LAYER ARCHITECTURAL REVIEW

This document provides a systematic, layer-by-layer breakdown of the SmartWare Modular Monolith. Use this to demonstrate your mastery of "Clean Architecture" and separation of concerns during an interview.

---

## 1. SmartInventory.API (The Presentation Layer)

**Purpose:** This is the entry point of the application. It is strictly responsible for routing HTTP requests, enforcing rate limits, parsing JSON payloads, validating authentication (JWTs), and mapping responses.

**Key Strengths Implemented:**
*   **API Versioning:** Using `[ApiVersion("1.0")]` and `api/v{version:apiVersion}/...` proves you are thinking about backwards compatibility for future enterprise clients.
*   **Rate Limiting:** `[EnableRateLimiting("auth")]` and `[EnableRateLimiting("mutations")]` defend against brute-force password attacks and DDoS attempts.
*   **Zero-Trust Bounded Contexts:** You explicitly separated `AuthController.cs` (internal employees) from `SupplierAuthController.cs` (external vendors), enforcing true Identity Isolation.

**Interviewer Attack:** *"Why do you have controllers if Minimal APIs are faster?"*
**Your Defense:** *"Minimal APIs are great for microservices, but for a massive ERP with hundreds of endpoints and complex `[Authorize(Policy="...")]` requirements, MVC Controllers provide better structural organization, easier testing via DI, and built-in model validation binding."*

---

## 2. SmartInventory.Core (The Domain Layer)

**Purpose:** The absolute center of the application. It has zero dependencies on any other project. It contains the Entities, Enums, Interfaces, and DTOs that define the business.

**Key Strengths Implemented:**
*   **Rich Domain Entities:** Your entities (like `Warehouse.cs` and `Supplier.cs`) are not just dumb data bags. They contain encrypted fields (`EncryptedTaxIdentifier`), status enums, and exact auditing fields.
*   **Dependency Inversion (Interfaces):** You defined `IUnitOfWork`, `IFileValidationService`, and `IAuthService` here. This forces the outer layers (Service/Repository) to depend on the Core, not the other way around.
*   **Soft Deletes:** `ISoftDelete` interface ensures financial records (like Products) are never `DROP`ped from the database, maintaining historical referential integrity.

**Interviewer Attack:** *"Why are your DTOs in the Core layer instead of the API layer?"*
**Your Defense:** *"By placing DTOs in the Core, the Service layer can consume and return them directly, meaning the Service layer doesn't need to return raw database Entities. This prevents accidental lazy-loading exceptions and leaking DB schemas to the API."*

---

## 3. SmartInventory.Repository (The Data Access Layer)

**Purpose:** Encapsulates all database interactions. The rest of the application does not know that Entity Framework Core exists.

**Key Strengths Implemented:**
*   **Generic Repository Pattern:** `GenericRepository<T>` prevents you from writing `db.Products.Add()` 500 times. It abstracts standard CRUD.
*   **Dynamic Expression Builder:** `ExpressionBuilder.cs` dynamically translates user search queries (from `DynamicQueryRequest`) into raw LINQ expression trees `Expression<Func<T, bool>>`. This is incredibly advanced and prevents SQL Injection while allowing flexible UI grids.
*   **Optimistic Concurrency:** The `AppDbContext` configures `xmin` as a concurrency token for PostgreSQL, guaranteeing race conditions are caught at the DB engine level.

**Interviewer Attack:** *"The Generic Repository is considered an anti-pattern when using EF Core because DbContext is already a repository. Why did you use it?"*
**Your Defense:** *"I used it to enforce the `IUnitOfWork` pattern explicitly and to hide `IQueryable` from the Service layer. If a service returns `IQueryable`, the query executes in the API layer during JSON serialization, which causes unpredictable performance. My repository forces materialization (`ToListAsync`) before returning data."*

---

## 4. SmartInventory.Infrastructure (The Cross-Cutting Layer)

**Purpose:** Handles technical concerns that don't belong to the business logic, such as Background Jobs, caching, and EF Core Interceptors.

**Key Strengths Implemented:**
*   **AuditInterceptor:** This is a masterpiece. Instead of writing `log.Info("PO updated")` in the controller, this hook intercepts `SaveChanges()`, extracts `OldValues` and `NewValues`, and writes them to the `AuditLogs` table in the exact same transaction.
*   **Quartz.NET / Background Services:** `AuditLogArchiveJob.cs` proves you know how to manage database bloat by asynchronously moving old logs to cold storage without blocking user HTTP threads.

**Interviewer Attack:** *"If your background job fails, how do you know?"*
**Your Defense:** *"Background jobs use `ILogger` to write to our centralized logging sink (like Serilog/Seq). Because it runs asynchronously, it won't crash the main API, but DevOps will be alerted via the error logs."*

---

## 5. SmartInventory.IntegrationTests

**Purpose:** Tests the application end-to-end, hitting a real (or containerized) database and Redis instance, ensuring the layers communicate correctly.

**Key Strengths Implemented:**
*   **Real Database Validation:** Unlike Unit Tests, this proves that EF Core migrations, PostgreSQL `xmin` concurrency, and database constraints actually work when a real HTTP request is made via `WebApplicationFactory`.

**Interviewer Attack:** *"Integration tests are slow. Why not just use InMemory database?"*
**Your Defense:** *"EF Core's InMemory provider is not a relational database. It ignores foreign key constraints, transaction boundaries, and raw SQL queries. Using TestContainers (Docker) spinning up a real PostgreSQL instance is the only way to guarantee the code works in production."*

---

## 6. SmartInventory.Service (The Business Logic Layer)

**Purpose:** The brain of the application. It orchestrates validation, enforces business rules, and coordinates the Repository and Infrastructure.

**Key Strengths Implemented:**
*   **JWT Rotation & Security:** `AuthService.cs` flawlessly implements Token Rotation. When a refresh token is used, it is instantly revoked.
*   **Security Validation:** `FileValidationService.cs` explicitly reads binary file streams (Magic Numbers) to prevent malware, completely ignoring the easily spoofed file extension.
*   **MediatR Decoupling:** `PurchaseOrderService.cs` updates the PO, saves it, and then simply publishes a `POApprovedEvent`. It doesn't care if an email is sent; the system handles that asynchronously.

**Interviewer Attack:** *"Your FileValidationService throws a `BinaryReader` exception if the file payload is empty. (As noted in the Code Review)."*
**Your Defense:** *"Yes, that is a known edge case. In production, I would add a strict `if (fileStream == null || fileStream.Length == 0) throw new BusinessRuleException(...)` at line 20 to gracefully handle malicious empty payloads."*

---

## 7. SmartInventory.Tests (The Unit Tests Layer)

**Purpose:** Lightning-fast tests that isolate the Service layer using Moq, ensuring business rules work without touching a database.

**Key Strengths Implemented:**
*   **Business Rule Coverage:** You can test that a Manager cannot approve their own Purchase Order simply by passing identical GUIDs to the Service method and asserting that a `BusinessRuleException` is thrown.
*   **Costing Logic:** You can assert that the Weighted Average Costing (WAC) math is absolutely perfect by feeding it mock inventory arrays.

**Interviewer Attack:** *"What is your code coverage percentage?"*
**Your Defense:** *"While I aim for high coverage, I prioritize testing critical business paths (like WAC calculations, Auth token generation, and Separation of Duties) over mindlessly testing getters and setters just to inflate a percentage metric."*
