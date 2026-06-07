namespace SmartInventory.Core.Exceptions;

/// <summary>
/// Base exception for all SmartInventory business exceptions.
/// </summary>
public abstract class SmartInventoryException : Exception
{
    public int StatusCode { get; }
    public string ErrorCode { get; }

    protected SmartInventoryException(string message, int statusCode, string errorCode)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Entity not found (HTTP 404).
/// </summary>
public class NotFoundException : SmartInventoryException
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} with identifier '{key}' was not found.", 404, "NOT_FOUND") { }
}

/// <summary>
/// Duplicate value conflict — e.g., duplicate SKU, email (HTTP 409).
/// </summary>
public class ConflictException : SmartInventoryException
{
    public ConflictException(string message)
        : base(message, 409, "CONFLICT") { }
}

/// <summary>
/// Business rule violation (HTTP 422).
/// </summary>
public class BusinessRuleException : SmartInventoryException
{
    public BusinessRuleException(string message)
        : base(message, 422, "BUSINESS_RULE_VIOLATION") { }
}

/// <summary>
/// Not enough stock for transfer/dispatch (HTTP 422).
/// </summary>
public class InsufficientStockException : SmartInventoryException
{
    public InsufficientStockException(string productName, int requested, int available)
        : base($"Insufficient stock for '{productName}'. Requested: {requested}, Available: {available}.",
            422, "INSUFFICIENT_STOCK") { }
}

/// <summary>
/// No permission for warehouse or action (HTTP 403).
/// </summary>
public class ForbiddenAccessException : SmartInventoryException
{
    public ForbiddenAccessException(string message = "You do not have permission to perform this action.")
        : base(message, 403, "FORBIDDEN") { }
}

/// <summary>
/// Input validation failed (HTTP 400).
/// </summary>
public class InputValidationException : SmartInventoryException
{
    public IDictionary<string, string[]> Errors { get; }

    public InputValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.", 400, "VALIDATION_FAILED")
    {
        Errors = errors;
    }
}

/// <summary>
/// Concurrency conflict — stale data (HTTP 409).
/// </summary>
public class StaleDataException : SmartInventoryException
{
    public int? CurrentQuantity { get; }

    public StaleDataException(string entityName, int? currentQuantity = null)
        : base($"The {entityName} was modified by another user. Please refresh and try again.",
            409, "STALE_DATA") 
    {
        CurrentQuantity = currentQuantity;
    }
}

/// <summary>
/// Action requires manager/admin approval (HTTP 202).
/// </summary>
public class ApprovalRequiredException : SmartInventoryException
{
    public ApprovalRequiredException(string action)
        : base($"The action '{action}' requires approval from a manager or admin. It has been saved as pending.",
            202, "APPROVAL_REQUIRED") { }
}
