using BAPStockManagement.Contracts.Dashboard;
using BAPStockManagement.Services.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BAPStockManagement.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/v1/notifications")]
public sealed class NotificationsApiController : ApiControllerBase
{
    private readonly IDashboardService _dashboardService;

    public NotificationsApiController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var summary = await _dashboardService.GetSummaryAsync(cancellationToken);
        return ApiOk(new UnreadNotificationCountDto(summary.UnreadNotificationCount));
    }
}
