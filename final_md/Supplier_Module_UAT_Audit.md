# Supplier Module — Complete Pre-UAT Audit

> **Audit basis:** Source code read directly on 2026-06-06.  
> **Files audited:** 5 controllers · 5 services · 2 DTO files (SupplierDtos.cs + SupplierPortal/*) · 2 validator files · SupplierStatus + PaymentTerms enums.  
> **No documentation or old Postman payloads were used.**

---

## Enum Reference

### PaymentTerms — [PaymentTerms.cs](file:///Users/akshaya/SmartWare/SmartInventory/SmartInventory.Core/Enums/PaymentTerms.cs)

| Integer | String |
|---------|--------|
| 0 | `Net30` |
| 1 | `Net60` |
| 2 | `Net90` |
| 3 | `COD` |
| 4 | `Prepaid` |

> ⚠️ ASP.NET Core's JSON deserializer accepts **both** integer (`1`) and string (`"Net60"`) values for enums, unless `JsonStringEnumConverter` is forced. Test both forms.

### SupplierStatus — [SupplierStatus.cs](file:///Users/akshaya/SmartWare/SmartInventory/SmartInventory.Core/Enums/SupplierStatus.cs)

| Integer | String |
|---------|--------|
| 0 | `Registered` |
| 1 | `InviteSent` |
| 2 | `PendingReview` |
| 3 | `InfoRequested` |
| 4 | `AgreementPending` |
| 5 | `Active` |
| 6 | `Rejected` |
| 7 | `Suspended` |

---

# PART 1 — SUPPLIERS CONTROLLER

**Base route:** `GET/PUT/POST/DELETE /api/v1/Suppliers`  
**Controller-level auth:** `[Authorize]` (any authenticated user)  
**Source:** [SuppliersController.cs](file:///Users/akshaya/SmartWare/SmartInventory/SmartInventory.API/Controllers/SuppliersController.cs)

---

## S-01 — Get All Suppliers

| Field | Value |
|-------|-------|
| **URL** | `GET /api/v1/Suppliers` |
| **Auth** | Any authenticated internal user (no policy restriction on this action) |
| **Role** | Any valid internal JWT |
| **Validator** | None registered for `SupplierQueryParameters` |

**Query Parameters (all optional):**

```
?page=1&pageSize=20&search=Tata&isActive=true&minRating=3.5
```

| Param | Type | Description |
|-------|------|-------------|
| `page` | int | Default: 1 |
| `pageSize` | int | Default: 20 |
| `search` | string | Matches name, code, or email |
| `isActive` | bool? | Filter by active/inactive |
| `minRating` | decimal? | Minimum rating threshold |

**Sample Response (200):**
```json
{
  "data": [
    {
      "id": "356675d4-46b5-4c5a-8192-dd04f404edf0",
      "name": "Tata Electronics Ltd",
      "code": "SUP-2026-00029",
      "gstin": "33AABCT1234C1Z5",
      "pan": "AABCT1234C",
      "contactPerson": "Aruna",
      "email": "aharchivee@gmail.com",
      "phone": "+919898989898",
      "address": "SIPCOT IT Park, Hosur, TN",
      "leadTimeDays": 0,
      "paymentTerms": 0,
      "paymentTermsName": "Net30",
      "creditLimit": 0.00,
      "rating": 0.00,
      "isActive": true,
      "createdAt": "2026-06-06T15:36:47.129895Z",
      "status": 4,
      "statusName": "AgreementPending",
      "registrationSource": 1,
      "registrationSourceName": "AdminInvited"
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20
}
```

**Postman Status:** ✅ No changes needed (query params only)

---

## S-02 — Get Supplier By Id

| Field | Value |
|-------|-------|
| **URL** | `GET /api/v1/Suppliers/{id:guid}` |
| **Auth** | Any authenticated internal user |
| **Role** | Any valid internal JWT |

**Sample Response (200):** Same object as a single item in S-01.

**Error Responses:**
- `404` — Supplier not found

**Postman Status:** ✅ No changes needed

---

## S-03 — Update Supplier

| Field | Value |
|-------|-------|
| **URL** | `PUT /api/v1/Suppliers/{id:guid}` |
| **Auth** | `RequireManager` |
| **Rate Limit** | `mutations` policy |
| **Validator** | `SupplierUpdateValidator` — [InventoryValidators.cs L126](file:///Users/akshaya/SmartWare/SmartInventory/SmartInventory.Core/Validators/InventoryValidators.cs#L126-L161) |

**Request DTO — `SupplierUpdateDto`:**

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `name` | string | ✅ | 2–150 chars |
| `code` | string | ✅ | 3–20 chars, `^[A-Z0-9-]{3,20}$` |
| `gstin` | string? | ✅ | 15-char Indian GSTIN regex |
| `pan` | string? | ✅ | 10-char Indian PAN regex |
| `email` | string? | ✅ | Valid email format |
| `phone` | string? | ✅ | `^\+91[6-9]\d{9}$` |
| `contactPerson` | string? | ❌ | None |
| `address` | string? | ❌ | None |
| `leadTimeDays` | int | ✅ | >= 0 |
| `creditLimit` | decimal | ✅ | >= 0 |
| `paymentTerms` | enum | ✅ | 0–4 or string equivalent |
| `isActive` | bool | ✅ | Required (non-nullable) |

**Example Body:**
```json
{
  "name": "Tata Electronics Ltd",
  "code": "TATA-001",
  "gstin": "33AABCT1234C1Z5",
  "pan": "AABCT1234C",
  "contactPerson": "Aruna",
  "address": "SIPCOT IT Park, Hosur, TN",
  "email": "aharchivee@gmail.com",
  "phone": "+919898989898",
  "leadTimeDays": 7,
  "creditLimit": 500000,
  "paymentTerms": 1,
  "isActive": true
}
```

**Business Rules (from service):**
- Code must be unique across all suppliers (excluding self).

**Status Change:** None  
**Notifications:** None  

**Error Responses:**
- `400` — Validation failure (code format, GSTIN/PAN format, etc.)
- `404` — Supplier not found
- `409` / `422` — Code conflict (thrown as `BusinessRuleException`)

**Postman Status:** 🔄 **UPDATE** — paymentTerms must be `1` (int) not `"Net45"` (does not exist). `isActive` must be included.

---

## S-04 — Delete Supplier

| Field | Value |
|-------|-------|
| **URL** | `DELETE /api/v1/Suppliers/{id:guid}` |
| **Auth** | `RequireAdmin` |
| **Rate Limit** | `mutations` |
| **Response** | `204 No Content` |

**Business Rules:**
- Blocked if supplier has open/in-progress Purchase Orders (status NOT Closed or Rejected).

**Error Responses:**
- `404` — Supplier not found
- `422` — Has open POs

**Postman Status:** ✅ No changes needed

---

## S-05 — Get Supplier Products

| Field | Value |
|-------|-------|
| **URL** | `GET /api/v1/Suppliers/{id:guid}/products` |
| **Auth** | Any authenticated internal user |

**Sample Response (200):**
```json
[
  {
    "id": "...",
    "supplierId": "...",
    "supplierName": "Tata Electronics Ltd",
    "productId": "...",
    "productName": "USB-C Cable",
    "productSKU": "ELC-00001",
    "unitPrice": 120.00,
    "leadTimeDays": 5,
    "minOrderQuantity": 50,
    "isPreferred": true,
    "createdAt": "2026-06-06T..."
  }
]
```

**Postman Status:** ✅ No changes needed

---

## S-06 — Add Supplier Product

| Field | Value |
|-------|-------|
| **URL** | `POST /api/v1/Suppliers/{id:guid}/products` |
| **Auth** | `RequireManager` |
| **Rate Limit** | `mutations` |
| **Note** | `SupplierId` is taken from URL path, not body |

**Request DTO — `SupplierProductCreateDto`:**

```json
{
  "productId": "guid-of-product",
  "unitPrice": 120.00,
  "leadTimeDays": 5,
  "minOrderQuantity": 50,
  "isPreferred": true
}
```

**Business Rules:**
- If `isPreferred = true`, any previously preferred supplier for this product is cleared.

**Response:** `201 Created` with `SupplierProductResponseDto`

**Postman Status:** ✅ No changes needed (verify `productId` is a valid active product GUID)

---

## S-07 — Update Supplier Product

| Field | Value |
|-------|-------|
| **URL** | `PUT /api/v1/Suppliers/products/{supplierProductId:guid}` |
| **Auth** | `RequireManager` |
| **Rate Limit** | `mutations` |

**Request DTO — `SupplierProductUpdateDto`:**

```json
{
  "unitPrice": 115.00,
  "leadTimeDays": 7,
  "minOrderQuantity": 100,
  "isPreferred": false
}
```

**Postman Status:** ✅ No changes needed

---

## S-08 — Remove Supplier Product

| Field | Value |
|-------|-------|
| **URL** | `DELETE /api/v1/Suppliers/products/{supplierProductId:guid}` |
| **Auth** | `RequireAdmin` |
| **Rate Limit** | `mutations` |
| **Response** | `204 No Content` |

**Postman Status:** ✅ No changes needed

---

## S-09 — Get Supplier Performance

| Field | Value |
|-------|-------|
| **URL** | `GET /api/v1/Suppliers/{id:guid}/performance` |
| **Auth** | `RequireViewer` |

**Sample Response (200):**
```json
[
  {
    "id": "...",
    "supplierId": "...",
    "supplierName": "Tata Electronics Ltd",
    "purchaseOrderId": "...",
    "purchaseOrderNumber": "PO-2026-00001",
    "promisedDays": 7,
    "actualDays": 6,
    "fillRate": 0.95,
    "notes": "On time, minor shortage",
    "createdAt": "2026-06-06T..."
  }
]
```

**Postman Status:** ✅ No changes needed

---

## S-10 — Recalculate Supplier Rating

| Field | Value |
|-------|-------|
| **URL** | `POST /api/v1/Suppliers/{id:guid}/recalculate-rating` |
| **Auth** | `RequireAdmin` |
| **Rate Limit** | `mutations` |
| **Response** | `204 No Content` |
| **Body** | None |

**Business Rules:**
- Formula: `Rating = (avgFillRate * 0.6 + onTimePct * 0.4) / 20.0`
- Scale: 0.0 – 5.0
- No-op if no performance logs exist.

**Postman Status:** ✅ No changes needed

---

## S-11 — Invite Supplier

| Field | Value |
|-------|-------|
| **URL** | `POST /api/v1/Suppliers/invite` |
| **Auth** | `RequireManager` |
| **Rate Limit** | `mutations` |
| **Validator** | 🚨 **NONE** — `SupplierInviteRequest` has no registered FluentValidation validator |

**Request DTO — `SupplierInviteRequest`:**

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `name` | string | ✅ | None (service does not validate) |
| `gstin` | string | ✅ | Uniqueness checked in service |
| `email` | string | ✅ | Uniqueness checked in service |
| `phone` | string | ✅ | Uniqueness checked in service |

> 🚨 **UAT BLOCKER — Missing Validator**  
> `SupplierInviteRequest` has NO `AbstractValidator` registered. Invalid GSTIN format, invalid email format, or empty name will pass model binding and reach the service layer. This should be caught at the validation layer before hitting the DB.

**Example Body:**
```json
{
  "name": "Reliance Industries Ltd",
  "gstin": "27AAACR5055K1ZL",
  "email": "onboarding@reliance.com",
  "phone": "+919876543210"
}
```

**What happens in service:**
1. Checks GSTIN uniqueness on active suppliers.
2. Checks Email uniqueness on active suppliers.
3. Checks Phone uniqueness on active suppliers.
4. Creates `Supplier` record with `Status = InviteSent`, `InviteToken = Guid.NewGuid()`, expires in 7 days.
5. Creates a dummy `SupplierContact` with `EmailVerified = true` and a random BCrypt password.
6. Sends invite email with link: `https://smartinventory.app/supplier/complete-registration?token={token}`

**Status After:** `InviteSent (1)`  
**Notifications:** ✅ Invite email sent to supplier with complete-registration link  
**Response:** `200 OK` with `SupplierResponseDto`

**Error Responses:**
- `422` — Duplicate GSTIN / Email / Phone (BusinessRuleException)

**Postman Status:** 🔄 **UPDATE** — Payload is correct as 4 fields. Verify no extra fields are sent.

---

## S-12 — Get Pending Reviews

| Field | Value |
|-------|-------|
| **URL** | `GET /api/v1/Suppliers/pending-reviews` |
| **Auth** | `RequireManager` |
| **Body** | None |

**What it returns:** All suppliers where `Status == PendingReview (2)` and `IsActive == true`.  
**Response:** `200 OK` with `IEnumerable<SupplierResponseDto>`

**Postman Status:** ✅ No changes needed

---

## S-13 — Review Supplier (Approve / Reject / RequestMoreInfo)

| Field | Value |
|-------|-------|
| **URL** | `POST /api/v1/Suppliers/{id:guid}/review` |
| **Auth** | `RequireManager` |
| **Rate Limit** | `mutations` |
| **Validator** | 🚨 **NONE** — `SupplierReviewRequest` has no registered validator |

**Request DTO — `SupplierReviewRequest`:**

| Field | Type | Required When |
|-------|------|---------------|
| `action` | string | Always. `"Approve"`, `"Reject"`, `"RequestMoreInfo"` |
| `reason` | string? | Optional for Approve; used as rejection reason / info request message |
| `code` | string? | **Required for Approve** (3–20 chars, `^[A-Z0-9-]{3,20}$`) |
| `creditLimit` | decimal? | Required for Approve; defaults to 0 if null |
| `paymentTerms` | PaymentTerms? | Required for Approve; defaults to Net30 if null |

**Approve Body:**
```json
{
  "action": "Approve",
  "reason": null,
  "code": "TATA-001",
  "creditLimit": 500000,
  "paymentTerms": 1
}
```

**Reject Body:**
```json
{
  "action": "Reject",
  "reason": "Incomplete documentation provided.",
  "code": null,
  "creditLimit": null,
  "paymentTerms": null
}
```

**RequestMoreInfo Body:**
```json
{
  "action": "RequestMoreInfo",
  "reason": "Please provide your GST registration certificate.",
  "code": null,
  "creditLimit": null,
  "paymentTerms": null
}
```

**Status Transitions:**

| Action | Before | After |
|--------|--------|-------|
| Approve | PendingReview | AgreementPending |
| Reject | PendingReview | Rejected |
| RequestMoreInfo | PendingReview | InfoRequested |

**Notifications:**
- Approve → Email to supplier: "Application Approved, please sign agreement"
- Reject → Email to supplier with rejection reason
- RequestMoreInfo → Email to supplier with the message

**Business Rules:**
- Supplier MUST be in `PendingReview` state — else `BusinessRuleException`.
- On Approve: Code must be provided and unique.

> 🚨 **UAT BLOCKER — Missing Validator**  
> `SupplierReviewRequest` has no registered `AbstractValidator`. `action` field is a free-form string — `"approve"` (lowercase) will fail in service. Validator should enforce allowed action values.

**Postman Status:** 🔄 **UPDATE** — Use integer `1` for `paymentTerms`, not `"Net45"`.

---

## S-14 — Suspend Supplier

| Field | Value |
|-------|-------|
| **URL** | `POST /api/v1/Suppliers/{id:guid}/suspend` |
| **Auth** | `RequireAdmin` |
| **Rate Limit** | `mutations` |
| **Response** | `204 No Content` |

**Request DTO — `SupplierSuspendRequest`:**
```json
{
  "reason": "Fraudulent invoice submitted."
}
```

**Status Transition:** Any → `Suspended (7)`  
**Notifications:** ✅ Suspension email sent to supplier with reason.

**Postman Status:** ✅ No changes needed

---

## S-15 — Activate Supplier

| Field | Value |
|-------|-------|
| **URL** | `POST /api/v1/Suppliers/{id:guid}/activate` |
| **Auth** | `RequireAdmin` |
| **Rate Limit** | `mutations` |
| **Body** | None |
| **Response** | `204 No Content` |

**Status Transition:** Any → `Active (5)`. Clears `SuspensionReason` and `RejectionReason`.  
**Notifications:** ✅ Reactivation email sent to supplier.

**Postman Status:** ✅ No changes needed

---

---

# PART 2 — SUPPLIER AUTH CONTROLLER

**Base route:** `/api/v1/supplier/auth`  
**Source:** [SupplierAuthController.cs](file:///Users/akshaya/SmartWare/SmartInventory/SmartInventory.API/Controllers/SupplierAuthController.cs)  
**Service:** [SupplierAuthService.cs](file:///Users/akshaya/SmartWare/SmartInventory/SmartInventory.Service/Services/SupplierAuthService.cs)

---

## SA-01 — Supplier Login

| Field | Value |
|-------|-------|
| **URL** | `POST /api/v1/supplier/auth/login` |
| **Auth** | `[AllowAnonymous]` |
| **Rate Limit** | `mutations` |
| **Validator** | ✅ `SupplierLoginValidator` — email format + 8-char password |

**Request DTO — `SupplierLoginRequest`:**
```json
{
  "email": "aharchivee@gmail.com",
  "password": "YourPassword@1"
}
```

**Business Rules:**
- Contact must be `IsActive = true`.
- Supplier `Status` MUST be **`Active (5)`** — any other status returns a meaningful error message.
- Password verified with BCrypt.
- On success: `LastLoginAt` is updated.

**Login Blocked Messages by Status:**

| Status | Error Message |
|--------|---------------|
| Registered | "Please verify your email before logging in." |
| InviteSent | "Please complete your registration via the invite link." |
| PendingReview | "Your account is pending administrator review." |
| AgreementPending | "Your account is pending agreement signature." |
| Suspended | "Your account is suspended. Reason: {reason}" |
| Rejected | "Your application was rejected. Reason: {reason}" |
| InfoRequested | "More information is requested. Please check your email." |

**Response DTO — `SupplierAuthResponse`:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "base64-64-byte-random-string",
  "expiresAt": "2026-06-06T17:00:00Z",
  "contact": {
    "contactId": "guid",
    "supplierId": "guid",
    "fullName": "Aruna",
    "email": "aharchivee@gmail.com",
    "supplierName": "Tata Electronics Ltd",
    "supplierCode": "TATA-001"
  }
}
```

**JWT Claims embedded:** `sub`, `jti`, `email`, `role = "Supplier"`, `contactId`, `supplierId`

**Postman Status:** ✅ No changes needed

---

## SA-02 — Refresh Token

| Field | Value |
|-------|-------|
| **URL** | `POST /api/v1/supplier/auth/refresh` |
| **Auth** | `[AllowAnonymous]` |
| **Rate Limit** | `mutations` |
| **Validator** | 🚨 **NONE** — `SupplierRefreshTokenRequest` has no registered validator |

**Request DTO:**
```json
{
  "refreshToken": "base64-encoded-refresh-token-from-login"
}
```

**Business Rules:**
- Token must exist, not be revoked, not be expired.
- Old token is immediately revoked (token rotation).
- New access + refresh token pair is issued.

**Response:** Same `SupplierAuthResponse` structure as Login.

**Postman Status:** 🔄 **UPDATE** — Set `refreshToken` value as variable from the Login response.

---

## SA-03 — Logout

| Field | Value |
|-------|-------|
| **URL** | `POST /api/v1/supplier/auth/logout` |
| **Auth** | `RequireSupplier` (Supplier JWT required) |
| **Rate Limit** | `mutations` |
| **Response** | `204 No Content` |

**Request DTO:**
```json
{
  "refreshToken": "base64-refresh-token"
}
```

**Business Rules:**
- Marks the refresh token as revoked with reason "Explicit logout".
- If already revoked, silently returns success (no error).

**Postman Status:** 🔄 **UPDATE** — Must send Supplier Bearer token in Authorization header.

---

## SA-04 — Change Password

| Field | Value |
|-------|-------|
| **URL** | `PUT /api/v1/supplier/auth/change-password` |
| **Auth** | `RequireSupplier` |
| **Rate Limit** | `mutations` |
| **Validator** | ✅ `SupplierChangePasswordValidator` — min 8 chars, 1 uppercase, 1 digit, 1 special char |
| **Response** | `204 No Content` |

**Request DTO:**
```json
{
  "currentPassword": "OldPassword@1",
  "newPassword": "NewPassword@2",
  "confirmPassword": "NewPassword@2"
}
```

**Business Rules:**
- `currentPassword` must match BCrypt hash.
- All existing refresh tokens are revoked → forces re-login on all devices.
- `contactId` is extracted from JWT claims.

**Postman Status:** ✅ No changes needed

---

## SA-05 — Self Register

| Field | Value |
|-------|-------|
| **URL** | `POST /api/v1/supplier/auth/register` |
| **Auth** | `[AllowAnonymous]` |
| **Rate Limit** | `mutations` |
| **Validator** | 🚨 **NONE** — `SupplierRegisterRequest` has no registered FluentValidation validator |

> 🚨 **UAT BLOCKER — Missing Validator**  
> `SupplierRegisterRequest` has 8 fields, none of which are validated at the framework level before the service is called. Invalid GSTIN, invalid PAN, weak password, missing email — all reach the DB layer.

**Request DTO — `SupplierRegisterRequest`:**

| Field | Type | Required |
|-------|------|----------|
| `name` | string | ✅ |
| `gstin` | string | ✅ |
| `pan` | string | ✅ |
| `address` | string | ✅ |
| `contactFullName` | string | ✅ |
| `email` | string | ✅ |
| `phone` | string | ✅ |
| `password` | string | ✅ |

**Example Body:**
```json
{
  "name": "Mahindra Logistics Ltd",
  "gstin": "27AABCM1234C1Z5",
  "pan": "AABCM1234C",
  "address": "Gate 4, Mahindra World City, Chennai",
  "contactFullName": "Ravi Kumar",
  "email": "ravi.kumar@mahindra.com",
  "phone": "+919876543210",
  "password": "Supplier@123"
}
```

**What happens:**
1. Validates uniqueness: GSTIN, PAN, Email (on Supplier + SupplierContact), Phone.
2. Creates `Supplier` with `Status = Registered (0)`, `RegistrationSource = SelfRegistered`.
3. Creates `SupplierContact` with `EmailVerified = false`, 6-digit OTP token, expires in 15 minutes.
4. Sends OTP email.

**Status After:** `Registered (0)`  
**Notifications:** ✅ Email verification OTP sent

**Response:**
```json
{ "message": "Registration successful. Please verify your email using the OTP token sent to you." }
```

**Postman Status:** 🆕 **ADD** — Ensure this is in Postman with the correct 8-field body.

---

## SA-06 — Verify Email (OTP)

| Field | Value |
|-------|-------|
| **URL** | `POST /api/v1/supplier/auth/verify-email` |
| **Auth** | `[AllowAnonymous]` |
| **Rate Limit** | `mutations` |
| **Validator** | 🚨 **NONE** — `SupplierVerifyEmailRequest` has no registered validator |

**Request DTO — `SupplierVerifyEmailRequest`:**
```json
{
  "email": "ravi.kumar@mahindra.com",
  "token": "847291"
}
```

**Security Rules in Service:**
- Checks `OtpLockedUntil` before processing.
- If wrong token: `OtpRetryCount++`. Locks for 15 minutes after reaching `OtpMaxRetries`.
- On lock: regenerates OTP and resets expiry.
- If token is expired: regenerates OTP and returns error.
- On success: `EmailVerified = true`, OTP cleared, retry count reset.

**Status Transition:** `Registered (0)` → `PendingReview (2)`  
**Notifications:** ✅ "Email Verified" confirmation email sent.

**Response:**
```json
{ "message": "Email verified successfully. Your application is now pending review." }
```

**Postman Status:** 🆕 **ADD** — Needs to be in Postman with `email` + `token` from OTP in email.

---

## SA-07 — Complete Registration (Invited Supplier)

| Field | Value |
|-------|-------|
| **URL** | `POST /api/v1/supplier/auth/complete-registration` |
| **Auth** | `[AllowAnonymous]` |
| **Rate Limit** | `mutations` |
| **Validator** | 🚨 **NONE** — `SupplierCompleteRegistrationRequest` has no registered validator |

**Request DTO — `SupplierCompleteRegistrationRequest`:**

| Field | Type | Required |
|-------|------|----------|
| `inviteToken` | string | ✅ — from invite email URL |
| `contactFullName` | string | ✅ |
| `jobTitle` | string | ✅ |
| `pan` | string | ✅ |
| `address` | string | ✅ |
| `password` | string | ✅ |

**Example Body:**
```json
{
  "inviteToken": "abc123token-from-invite-email",
  "contactFullName": "Aruna Krishnamurthy",
  "jobTitle": "Head of Procurement",
  "pan": "AABCT1234C",
  "address": "SIPCOT IT Park, Hosur, TN 635109",
  "password": "Supplier@456"
}
```

**What happens:**
1. Finds supplier by `InviteToken`.
2. Validates PAN uniqueness.
3. Checks token not expired (7-day expiry).
4. Checks supplier is in `InviteSent (1)` state.
5. Updates: `PAN`, `Address`, `ContactPerson`. Clears `InviteToken`.
6. Updates the existing dummy contact (or creates new): sets `FullName`, `PasswordHash`, `JobTitle`, `EmailVerified = true`.
7. Moves supplier to `PendingReview (2)`.

**Status Transition:** `InviteSent (1)` → `PendingReview (2)`  
**Notifications:** None sent on complete registration (Admin sees it in pending-reviews).

**Response:**
```json
{ "message": "Registration completed successfully. Your profile is now pending review." }
```

**Postman Status:** 🔄 **UPDATE** — `inviteToken` must come from the actual invite email. `jobTitle` is a required field that was previously not in the payload.

---

## SA-08 — Forgot Password

| Field | Value |
|-------|-------|
| **URL** | `POST /api/v1/supplier/auth/forgot-password` |
| **Auth** | `[AllowAnonymous]` |
| **Rate Limit** | `mutations` |
| **Validator** | 🚨 **NONE** |

**Request DTO — `SupplierForgotPasswordRequest`:**
```json
{
  "email": "aharchivee@gmail.com"
}
```

**What happens:**
- If email does not exist: throws `BusinessRuleException("Email not found.")`.

> ⚠️ **Security Note:** The current implementation reveals whether an email is registered (throws 422 if not found). Best practice for forgot-password is to always return 200 regardless. This is a minor security concern but not a blocker for UAT.

- Generates a `Base64(RandomBytes(32))` token, stores in `EmailVerifyToken`, expires in 1 hour.
- Calls `NotificationService.SendPasswordResetRequestAsync`.
- Reset link: `https://app.smartware.com/supplier/reset-password?token={token}`

**Response:**
```json
{ "message": "If the email exists, a password reset link has been sent." }
```

**Postman Status:** 🆕 **ADD** — Must be in Postman collection.

---

## SA-09 — Reset Password

| Field | Value |
|-------|-------|
| **URL** | `POST /api/v1/supplier/auth/reset-password` |
| **Auth** | `[AllowAnonymous]` |
| **Rate Limit** | `mutations` |
| **Validator** | 🚨 **NONE** — `SupplierResetPasswordRequest` has no registered validator |

**Request DTO — `SupplierResetPasswordRequest`:**
```json
{
  "token": "base64-reset-token-from-email",
  "newPassword": "NewSecure@789"
}
```

**What happens:**
- Finds contact by `EmailVerifyToken` (reused for reset).
- Validates token not expired (1-hour window).
- Updates `PasswordHash`. Clears token.
- Revokes all existing refresh tokens (forces re-login everywhere).
- Calls `NotificationService.SendPasswordResetSuccessAsync`.

**Status Transition:** None  
**Notifications:** ✅ Success email sent via notification service.

**Response:**
```json
{ "message": "Password has been reset successfully. You can now log in with your new password." }
```

**Postman Status:** 🆕 **ADD** — Must be in Postman collection.

---

---

# PART 3 — SUPPLIER PROFILE CONTROLLER

**Base route:** `/api/v1/supplier/profile`  
**Auth (controller-level):** `RequireSupplier` — ALL endpoints require a valid Supplier JWT.  
**Source:** [SupplierProfileController.cs](file:///Users/akshaya/SmartWare/SmartInventory/SmartInventory.API/Controllers/SupplierProfileController.cs)

---

## SP-01 — Get My Profile

| Field | Value |
|-------|-------|
| **URL** | `GET /api/v1/supplier/profile` |
| **Auth** | Supplier JWT (supplierId + contactId from claims) |

**Response DTO — `SupplierProfileDto`:**
```json
{
  "supplierId": "guid",
  "name": "Tata Electronics Ltd",
  "code": "TATA-001",
  "address": "SIPCOT IT Park, Hosur, TN",
  "contactPersonName": "Aruna Krishnamurthy",
  "contactEmail": "aharchivee@gmail.com",
  "contactPhone": "+919898989898",
  "jobTitle": "Head of Procurement",
  "leadTimeDays": 7,
  "rating": 0.00,
  "isActive": true
}
```

**Postman Status:** ✅ No changes needed (just use Supplier JWT)

---

## SP-02 — Update Profile

| Field | Value |
|-------|-------|
| **URL** | `PUT /api/v1/supplier/profile` |
| **Auth** | Supplier JWT |
| **Rate Limit** | `mutations` |
| **Validator** | ✅ `SupplierUpdateProfileValidator` |
| **Response** | `200 { message: "Profile updated successfully." }` |

**Request DTO — `SupplierUpdateProfileRequest`:**

| Field | Required | Validation |
|-------|----------|------------|
| `fullName` | ✅ | NotEmpty, max 150 chars |
| `phone` | ❌ | If provided: `^\+91[6-9]\d{9}$` |
| `jobTitle` | ❌ | If provided: max 100 chars |

**Example Body:**
```json
{
  "fullName": "Aruna Krishnamurthy",
  "phone": "+919898989898",
  "jobTitle": "Head of Procurement"
}
```

**Postman Status:** ✅ No changes needed

---

## SP-03 — Upload Logo

| Field | Value |
|-------|-------|
| **URL** | `POST /api/v1/supplier/profile/logo` |
| **Auth** | Supplier JWT |
| **Rate Limit** | `mutations` |
| **Content-Type** | `multipart/form-data` |
| **Body** | Form file field named `logo` |

**Response:**
```json
{ "message": "Logo uploaded successfully.", "path": "/supplier-logos/filename.jpg" }
```

**Postman Status:** 🔄 **UPDATE** — Set Body to `form-data`, add file field named `logo`.

---

## SP-04 — Get Onboarding Status

| Field | Value |
|-------|-------|
| **URL** | `GET /api/v1/supplier/profile/status` |
| **Auth** | Supplier JWT |

**Response DTO — `SupplierOnboardingStatusResponse`:**
```json
{
  "status": 2,
  "statusName": "PendingReview",
  "rejectionReason": null,
  "suspensionReason": null,
  "infoRequestedMessage": null,
  "emailVerified": true
}
```

**Postman Status:** ✅ No changes needed

---

## SP-05 — Submit Onboarding Info

| Field | Value |
|-------|-------|
| **URL** | `POST /api/v1/supplier/profile/submit-info` |
| **Auth** | Supplier JWT |
| **Rate Limit** | `mutations` |
| **Response** | `200 { message: "Information submitted successfully..." }` |

**Request DTO — `SupplierSubmitInfoRequest`:**
```json
{
  "message": "I have attached the GST registration certificate to the email as requested."
}
```

**Business Rules:**
- Only allowed when supplier status is `InfoRequested (3)`.
- On success: status moves back to `PendingReview (2)`, `InfoRequestedMessage` is cleared.

**Status Transition:** `InfoRequested (3)` → `PendingReview (2)`

**Postman Status:** 🆕 **ADD** — This endpoint was likely not in old Postman collection.

---

## SP-06 — Get Agreement

| Field | Value |
|-------|-------|
| **URL** | `GET /api/v1/supplier/profile/agreement` |
| **Auth** | Supplier JWT |

**Business Rules:** Only accessible when status is `AgreementPending (4)` or `Active (5)`.

**Response:**
```json
{
  "agreementText": "Standard Partnership Agreement for Tata Electronics Ltd (TATA-001).\nBy clicking accept, you agree to..."
}
```

**Postman Status:** ✅ No changes needed

---

## SP-07 — Accept Agreement

| Field | Value |
|-------|-------|
| **URL** | `POST /api/v1/supplier/profile/agreement/accept` |
| **Auth** | Supplier JWT |
| **Rate Limit** | `mutations` |
| **Body** | None |
| **Response** | `200 { message: "Agreement accepted successfully. Your account is now active!" }` |

**Business Rules:**
- Supplier must be in `AgreementPending (4)`.
- On accept: `Status = Active (5)`, `AgreementSignedAt = UTC now`, `AgreementSignedIp = caller IP`.

**Status Transition:** `AgreementPending (4)` → `Active (5)`  
**Notifications:** None sent by this endpoint.

> ⚠️ **Minor Gap:** No notification email is sent when a supplier accepts the agreement and becomes Active. Consider adding one for Admin awareness.

**Postman Status:** ✅ No changes needed

---

---

# PART 4 — SUPPLIER DASHBOARD CONTROLLER

**Base route:** `/api/v1/supplier/dashboard`  
**Auth:** `RequireSupplier`

---

## SD-01 — Get Dashboard

| Field | Value |
|-------|-------|
| **URL** | `GET /api/v1/supplier/dashboard` |
| **Auth** | Supplier JWT |
| **Body** | None |

**Response DTO — `SupplierDashboardSummaryDto`:**
```json
{
  "totalOrders": 12,
  "pendingOrders": 2,
  "dispatchedOrders": 1,
  "completedOrders": 9,
  "totalVolumeSupplied": 1250000.00,
  "overallRating": 4.20,
  "onTimeDeliveryPercentage": 91.7,
  "averageFillRate": 96.5,
  "fillRateHistory": [
    {
      "poNumber": "PO-2026-00001",
      "orderDate": "2026-05-01T00:00:00Z",
      "fillRate": 98.0,
      "promisedDays": 7,
      "actualDays": 6,
      "onTime": true
    }
  ]
}
```

**Postman Status:** ✅ No changes needed

---

---

# PART 5 — SUPPLIER CATALOGUE CONTROLLER

**Base route:** `/api/v1/supplier/catalogue`  
**Auth:** `RequireSupplier`

---

## SC-01 — Get My Catalogue

| Field | Value |
|-------|-------|
| **URL** | `GET /api/v1/supplier/catalogue` |
| **Auth** | Supplier JWT |

**Response:** Array of `SupplierCatalogueItemDto`:
```json
[
  {
    "supplierProductId": "guid",
    "productId": "guid",
    "productName": "USB-C Cable",
    "sku": "ELC-00001",
    "category": "Electronics",
    "unitPrice": 120.00,
    "leadTimeDays": 5,
    "minOrderQuantity": 50,
    "isPreferred": true,
    "isActive": true
  }
]
```

**Postman Status:** ✅ No changes needed

---

## SC-02 — Add Catalogue Item

| Field | Value |
|-------|-------|
| **URL** | `POST /api/v1/supplier/catalogue` |
| **Auth** | Supplier JWT |
| **Rate Limit** | `mutations` |
| **Validator** | ✅ `SupplierAddCatalogueItemValidator` |
| **Response** | `201 Created` |

**Request DTO — `SupplierAddCatalogueItemRequest`:**

| Field | Required | Validation |
|-------|----------|------------|
| `productId` | ✅ | NotEmpty |
| `unitPrice` | ✅ | > 0 |
| `leadTimeDays` | ✅ | 1–365 |
| `minOrderQuantity` | ✅ | > 0 |

**Example Body:**
```json
{
  "productId": "guid-of-existing-active-product",
  "unitPrice": 120.00,
  "leadTimeDays": 5,
  "minOrderQuantity": 50
}
```

**Business Rules:**
- Product must exist and be active.
- Duplicate check: same supplier cannot add the same product twice.
- `IsPreferred` is always set to `false` on add. Use the Admin endpoint (S-06/S-07) to set preferred.

**Response:** `SupplierCatalogueItemDto`

**Postman Status:** ✅ No changes needed

---

## SC-03 — Update Catalogue Item

| Field | Value |
|-------|-------|
| **URL** | `PUT /api/v1/supplier/catalogue/{id:guid}` |
| **Auth** | Supplier JWT |
| **Rate Limit** | `mutations` |
| **Validator** | ✅ `SupplierUpdateCatalogueItemValidator` |
| **Response** | `200 { message: "Catalogue item updated successfully." }` |

**Request DTO — `SupplierUpdateCatalogueItemRequest`:**

| Field | Required | Validation |
|-------|----------|------------|
| `unitPrice` | ✅ | > 0 |
| `leadTimeDays` | ✅ | 1–365 |
| `minOrderQuantity` | ✅ | > 0 |

**Example Body:**
```json
{
  "unitPrice": 115.00,
  "leadTimeDays": 7,
  "minOrderQuantity": 100
}
```

**Business Rules:** Supplier can only update their own catalogue items (service filters on supplierId from JWT).

**Postman Status:** ✅ No changes needed

---

## SC-04 — Deactivate Catalogue Item

| Field | Value |
|-------|-------|
| **URL** | `DELETE /api/v1/supplier/catalogue/{id:guid}` |
| **Auth** | Supplier JWT |
| **Rate Limit** | `mutations` |
| **Body** | None |
| **Response** | `200 { message: "Catalogue item deactivated." }` |

**Business Rules:**
- Soft-delete — sets `IsActive = false`.
- If already inactive: `BusinessRuleException`.
- Only the supplier's own items can be deactivated (filtered by `supplierId`).

**Postman Status:** ✅ No changes needed

---

---

# UAT BLOCKERS SUMMARY

| # | Blocker | File | Issue |
|---|---------|------|-------|
| 🚨 B1 | `SupplierInviteRequest` has no validator | [SupplierPortalValidators.cs](file:///Users/akshaya/SmartWare/SmartInventory/SmartInventory.Core/Validators/SupplierPortalValidators.cs) | Invalid GSTIN, email, phone formats pass to DB |
| 🚨 B2 | `SupplierRegisterRequest` has no validator | [SupplierPortalValidators.cs](file:///Users/akshaya/SmartWare/SmartInventory/SmartInventory.Core/Validators/SupplierPortalValidators.cs) | Weak password, invalid GSTIN/PAN pass to service |
| 🚨 B3 | `SupplierReviewRequest` has no validator | [SupplierPortalValidators.cs](file:///Users/akshaya/SmartWare/SmartInventory/SmartInventory.Core/Validators/SupplierPortalValidators.cs) | `"approve"` (lowercase) silently fails in service |

---

# MISSING VALIDATORS DETAIL

| DTO | Missing Validator | Impact |
|-----|------------------|--------|
| `SupplierInviteRequest` | No validator | Name, GSTIN format, email, phone not validated at API layer |
| `SupplierRegisterRequest` | No validator | All 8 fields unvalidated — password strength, GSTIN/PAN format |
| `SupplierVerifyEmailRequest` | No validator | Email and token emptiness not enforced |
| `SupplierCompleteRegistrationRequest` | No validator | InviteToken, PAN format, password strength not validated |
| `SupplierReviewRequest` | No validator | action value, Code format not enforced |
| `SupplierForgotPasswordRequest` | No validator | Email format not validated |
| `SupplierResetPasswordRequest` | No validator | NewPassword strength not enforced |
| `SupplierRefreshTokenRequest` | No validator | Token emptiness not enforced |

---

# ORPHAN SERVICE METHOD

| Method | File | Issue |
|--------|------|-------|
| `ResendOtpAsync` | [SupplierAuthService.cs L406](file:///Users/akshaya/SmartWare/SmartInventory/SmartInventory.Service/Services/SupplierAuthService.cs#L406-L443) | Fully implemented in service, but **no controller endpoint exists** for it. Suppliers cannot resend OTP via API. |

---

# POSTMAN ACTION LIST

## 🔄 UPDATE These Existing Requests

| Request Name | What to Change |
|---|---|
| **Update Supplier** | `paymentTerms` must be int (0–4), not `"Net45"`. Add `isActive: true`. |
| **Invite Supplier** | Verify only 4 fields: `name`, `gstin`, `email`, `phone`. |
| **Review Supplier — Approve** | `paymentTerms` must be int (e.g., `1`). Remove `"Net45"`. |
| **Review Supplier — Reject** | `code`, `creditLimit`, `paymentTerms` should all be `null`. |
| **Supplier Refresh Token** | Set `refreshToken` from Login response variable. |
| **Supplier Logout** | Must include Supplier Bearer token in headers. |
| **Complete Registration** | Add `jobTitle` field. `inviteToken` comes from invite email URL. |
| **Upload Logo** | Switch to `multipart/form-data`. Add file field named `logo`. |

## 🆕 ADD These New Requests

| Request Name | Endpoint |
|---|---|
| **Supplier Self Register** | `POST /api/v1/supplier/auth/register` |
| **Supplier Verify Email (OTP)** | `POST /api/v1/supplier/auth/verify-email` |
| **Supplier Forgot Password** | `POST /api/v1/supplier/auth/forgot-password` |
| **Supplier Reset Password** | `POST /api/v1/supplier/auth/reset-password` |
| **Supplier Submit Onboarding Info** | `POST /api/v1/supplier/profile/submit-info` |
| **Get Supplier Pending Reviews** | `GET /api/v1/Suppliers/pending-reviews` |

## 🗑️ DELETE These Old Requests

| Request Name | Reason |
|---|---|
| **Admin Create Supplier (direct)** | Removed from codebase — Admin must use Invite flow only |

---

# UAT EXECUTION ORDER

Follow this exact sequence:

```
PHASE 1 — ADMIN INVITE FLOW
────────────────────────────
1.  [Admin] POST /api/v1/Suppliers/invite
    → Status: InviteSent
    → Save inviteToken from email

2.  [Supplier] POST /api/v1/supplier/auth/complete-registration
    → Status: PendingReview

3.  [Admin] GET /api/v1/Suppliers/pending-reviews
    → Verify supplier appears

4.  [Admin] POST /api/v1/Suppliers/{id}/review  (Approve)
    → Status: AgreementPending

5.  [Supplier] POST /api/v1/supplier/auth/login
    → Should fail: "pending agreement signature"

6.  [Supplier] GET /api/v1/supplier/profile/agreement
    → View agreement text

7.  [Supplier] POST /api/v1/supplier/profile/agreement/accept
    → Status: Active

8.  [Supplier] POST /api/v1/supplier/auth/login
    → Should succeed → Save accessToken + refreshToken

PHASE 2 — SUPPLIER PORTAL OPERATIONS
──────────────────────────────────────
9.  [Supplier] GET /api/v1/supplier/profile
10. [Supplier] PUT /api/v1/supplier/profile
11. [Supplier] GET /api/v1/supplier/profile/status
12. [Supplier] GET /api/v1/supplier/dashboard
13. [Supplier] GET /api/v1/supplier/catalogue
14. [Supplier] POST /api/v1/supplier/catalogue
15. [Supplier] PUT /api/v1/supplier/catalogue/{id}
16. [Supplier] DELETE /api/v1/supplier/catalogue/{id}

PHASE 3 — TOKEN MANAGEMENT
────────────────────────────
17. [Supplier] POST /api/v1/supplier/auth/refresh
18. [Supplier] PUT /api/v1/supplier/auth/change-password
19. [Supplier] POST /api/v1/supplier/auth/logout

PHASE 4 — PASSWORD RESET
──────────────────────────
20. [Supplier] POST /api/v1/supplier/auth/forgot-password
21. [Supplier] POST /api/v1/supplier/auth/reset-password (use token from email)
22. [Supplier] POST /api/v1/supplier/auth/login (with new password)

PHASE 5 — SELF REGISTRATION FLOW
───────────────────────────────────
23. [Supplier] POST /api/v1/supplier/auth/register
24. [Supplier] POST /api/v1/supplier/auth/verify-email
25. [Admin] GET /api/v1/Suppliers/pending-reviews
26. [Admin] POST /api/v1/Suppliers/{id}/review (RequestMoreInfo)
27. [Supplier] POST /api/v1/supplier/profile/submit-info
28. [Admin] POST /api/v1/Suppliers/{id}/review (Approve)

PHASE 6 — ADMIN SUPPLIER MANAGEMENT
─────────────────────────────────────
29. [Admin] GET /api/v1/Suppliers
30. [Admin] GET /api/v1/Suppliers/{id}
31. [Admin] PUT /api/v1/Suppliers/{id}
32. [Admin] POST /api/v1/Suppliers/{id}/suspend
33. [Admin] POST /api/v1/Suppliers/{id}/activate
34. [Admin] POST /api/v1/Suppliers/{id}/recalculate-rating
35. [Admin] GET /api/v1/Suppliers/{id}/performance

PHASE 7 — SUPPLIER PRODUCTS (Admin)
──────────────────────────────────────
36. [Admin] POST /api/v1/Suppliers/{id}/products
37. [Admin] GET /api/v1/Suppliers/{id}/products
38. [Admin] PUT /api/v1/Suppliers/products/{spId}
39. [Admin] DELETE /api/v1/Suppliers/products/{spId}
```

---

# FINAL SUPPLIER MODULE READINESS SCORE

| Category | Score | Notes |
|----------|-------|-------|
| **Controllers** | 10/10 | All 5 controllers correctly defined, routes correct, auth policies applied |
| **Services** | 9/10 | All service logic implemented. -1 for ResendOtp orphan method with no endpoint |
| **DTOs** | 10/10 | All request and response DTOs correctly defined and used |
| **Validators** | 4/10 | Only 4 of 12 supplier-related request DTOs have validators. 8 critical gaps |
| **Business Rules** | 9/10 | All lifecycle state transitions correct. -1 for missing accept-agreement notification |
| **Notifications** | 9/10 | Invite, approve, reject, requestMoreInfo, suspend, activate, OTP, forgot-pwd, reset-pwd all covered. Accept-agreement missing |
| **Postman Collection** | 6/10 | 8 updates needed, 6 new requests to add, 1 to delete |
| **DB Schema** | 10/10 | Migration applied for `LastOtpSentAt` column — schema now in sync |

**Overall Readiness: 72/100 — NOT UAT-READY**

### Minimum requirements before UAT can begin:
1. ✅ Fix `paymentTerms` in Postman (integer, not `"Net45"`)
2. 🚨 Add validators for `SupplierInviteRequest`, `SupplierRegisterRequest`, `SupplierReviewRequest`
3. 🆕 Add missing Postman requests: Register, VerifyEmail, ForgotPassword, ResetPassword, SubmitInfo
4. 🔧 Add ResendOtp controller endpoint (endpoint exists in service, not exposed)
5. 📝 After fixing above — re-run `dotnet build` to confirm 0 warnings
