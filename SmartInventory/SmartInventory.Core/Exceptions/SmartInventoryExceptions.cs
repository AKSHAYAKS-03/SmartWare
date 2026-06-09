namespace SmartInventory.Core.Exceptions;


//// Base exception for all SmartInventory business exceptions.
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


//// Entity not found (HTTP 404).
public class NotFoundException : SmartInventoryException
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} with identifier '{key}' was not found.", 404, "NOT_FOUND") { }
}


//// Duplicate value conflict — e.g., duplicate SKU, email (HTTP 409).
public class ConflictException : SmartInventoryException
{
    public ConflictException(string message)
        : base(message, 409, "CONFLICT") { }
}


//// Barcode already exists for a product (HTTP 409).
public class BarcodeAlreadyExistsException : SmartInventoryException
{
    public BarcodeAlreadyExistsException()
        : base("Product already has a barcode. Use the update barcode endpoint if changes are required.", 409, "BARCODE_ALREADY_EXISTS")
    {
    }
}


//// Business rule violation (HTTP 422).
public class BusinessRuleException : SmartInventoryException
{
    public BusinessRuleException(string message)
        : base(message, 422, "BUSINESS_RULE_VIOLATION") { }
}


//// Not enough stock for transfer/dispatch (HTTP 422).
public class InsufficientStockException : SmartInventoryException
{
    public InsufficientStockException(string productName, int requested, int available)
        : base($"Insufficient stock for '{productName}'. Requested: {requested}, Available: {available}.",
            422, "INSUFFICIENT_STOCK") { }
}


//// No permission for warehouse or action (HTTP 403).
public class ForbiddenAccessException : SmartInventoryException
{
    public ForbiddenAccessException(string message = "You do not have permission to perform this action.")
        : base(message, 403, "FORBIDDEN") { }
}


//// Input validation failed (HTTP 400).
public class InputValidationException : SmartInventoryException
{
    public IDictionary<string, string[]> Errors { get; }

    public InputValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.", 400, "VALIDATION_FAILED")
    {
        Errors = errors;
    }
}


//// Concurrency conflict — stale data (HTTP 409).
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


// Action requires manager//admin approval (HTTP 202).
public class ApprovalRequiredException : SmartInventoryException
{
    public ApprovalRequiredException(string action)
        : base($"The action '{action}' requires approval from a manager or admin. It has been saved as pending.",
            202, "APPROVAL_REQUIRED") { }
}
