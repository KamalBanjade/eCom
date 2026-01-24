using Commerce.Domain.Enums;
using Commerce.Application.Features.Orders.DTOs;

namespace Commerce.Application.Features.Returns.DTOs;

/// <summary>
/// DTO for return request with order and customer details
/// </summary>
public class ReturnRequestDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string OrderPaymentMethod { get; set; } = string.Empty;
    public decimal OrderTotalAmount { get; set; }
    public decimal OrderDiscountAmount { get; set; }
    
    public string Reason { get; set; } = string.Empty;
    public string ReturnStatus { get; set; } = string.Empty;
    
    public decimal RefundAmount { get; set; }
    public string? RefundMethod { get; set; }
    
    public string? KhaltiPidx { get; set; }
    
    // Assignment tracking
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedRole { get; set; }
    public string? AssignedToUserEmail { get; set; }
    public DateTime? AssignedAt { get; set; }
    
    // Timestamps
    public DateTime RequestedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? PickedUpAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? InspectionCompletedAt { get; set; }
    public DateTime? ProcessingAt { get; set; } // Compatibility/Internal
    public DateTime? RefundedAt { get; set; }
    
    // Customer info
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    
    // Items snapshot from Order + Return details
    public List<ReturnItemDto> Items { get; set; } = new();
}

/// <summary>
/// Filter request for return requests
/// </summary>
public class ReturnFilterRequest
{
    public ReturnStatus? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? OrderNumber { get; set; }
    public string? CustomerSearch { get; set; } // Name or email
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

/// <summary>
/// Request to assign a return to a user
/// </summary>
public class AssignReturnRequest
{
    public Guid AssignedToUserId { get; set; }
}
