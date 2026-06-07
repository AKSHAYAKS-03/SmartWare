using System;
using System.Collections.Generic;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.DTOs;

public class InvoiceActionDto
{
    public string? InternalNotes { get; set; }
}

public class InvoiceRejectDto : InvoiceActionDto
{
    public string RejectionReason { get; set; } = string.Empty;
}

public class InvoicePayDto : InvoiceActionDto
{
    public string PaymentReference { get; set; } = string.Empty;
}

public class InvoiceMatchResultDto
{
    public Guid InvoiceId { get; set; }
    public bool IsMatch { get; set; }
    public List<string> Discrepancies { get; set; } = [];
    public decimal ApprovedAmount { get; set; }
}
