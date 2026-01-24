using Commerce.Application.Common.DTOs;
using Commerce.Application.Features.Dashboard;
using Commerce.Application.Features.Dashboard.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.API.Controllers;

[Route("api/admin/dashboard")]
[ApiController]
[Authorize(Roles = "Admin,SuperAdmin,Support,Warehouse")]
public class AdminDashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public AdminDashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// Admin: Retrieves comprehensive dashboard data with aggregated insights
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<DashboardDataDto>>> GetDashboardData(
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        var result = await _dashboardService.GetDashboardDataAsync(days, cancellationToken);
        return Ok(ApiResponse<DashboardDataDto>.SuccessResponse(result));
    }
}
