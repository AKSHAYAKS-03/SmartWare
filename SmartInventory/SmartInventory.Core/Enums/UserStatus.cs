namespace SmartInventory.Core.Enums;

/// <summary>
/// Professional corporate onboarding and vetting status of a system user.
/// </summary>
public enum UserStatus
{
    PendingVerification = 0, // Registered, but undergoing HR/IT/Security official verification
    Active = 1,              // Officially vetted, approved, and granted access to the site
    Suspended = 2,           // Temporarily locked out of the site due to audits or security locks
    Terminated = 3           // Officially offboarded from the company; hard lockout
}
