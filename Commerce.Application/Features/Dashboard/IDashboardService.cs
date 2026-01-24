using Commerce.Application.Features.Dashboard.DTOs;

namespace Commerce.Application.Features.Dashboard;

public interface IDashboardService
{
    Task<DashboardDataDto> GetDashboardDataAsync(int days = 30, CancellationToken cancellationToken = default);
}
