using Commerce.Application.Common.DTOs;
using Commerce.Application.Features.Orders.DTOs;
using Commerce.Application.Features.Returns.DTOs;
using Commerce.Domain.Entities.Sales;
using Commerce.Domain.Enums;

namespace Commerce.Application.Common.Interfaces;

public interface IReturnService
{
    Task<ApiResponse<ReturnRequest>> RequestReturnAsync(Guid orderId, List<CreateReturnItemDto> items, Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ReturnRequest>> GetUserReturnsAsync(Guid userId, CancellationToken cancellationToken = default);
    
    // Admin Methods
    Task<PagedResult<ReturnRequestDto>> GetAllReturnsAsync(ReturnFilterRequest filter, CancellationToken cancellationToken = default);
    Task<ReturnRequestDto?> GetReturnByIdAsync(Guid returnId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ReturnRequest>> AssignReturnAsync(Guid returnId, Guid assignedToUserId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ReturnRequest>> ApproveReturnAsync(Guid returnId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ReturnRequest>> RejectReturnAsync(Guid returnId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ReturnRequest>> MarkPickedUpAsync(Guid returnId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ReturnRequest>> MarkReceivedAsync(Guid returnId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ReturnRequest>> CompleteInspectionAsync(Guid returnId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ReturnRequest>> ProcessRefundAsync(Guid returnId, RefundMethod method, decimal amount, CancellationToken cancellationToken = default);
}
