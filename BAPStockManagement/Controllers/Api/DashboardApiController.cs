using System.ComponentModel.DataAnnotations;
using System.Globalization;
using BAPStockManagement.Contracts.Dashboard;
using BAPStockManagement.Services.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BAPStockManagement.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/v1/dashboard")]
public sealed class DashboardApiController : ApiControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardApiController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("summary")]
    [ResponseCache(Duration = 30, Location = ResponseCacheLocation.Client, VaryByHeader = "Accept")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var summary = await _dashboardService.GetSummaryAsync(cancellationToken);
        return ApiOkCached(summary);
    }

    [HttpGet("forecast")]
    [ResponseCache(Duration = 30, Location = ResponseCacheLocation.Client, VaryByHeader = "Accept")]
    public async Task<IActionResult> GetForecast(
        CancellationToken cancellationToken,
        [FromQuery]
        [RegularExpression("^(3m|6m|12m)$")]
        string horizon = "6m")
    {
        var summary = await _dashboardService.GetSummaryAsync(cancellationToken);
        var forecastStart = summary.Forecast
            .Select((point, index) => (point, index))
            .Where(item => item.point.Forecast.HasValue)
            .Select(item => item.index)
            .DefaultIfEmpty(-1)
            .First();
        var horizonMonths = int.Parse(
            horizon[..^1],
            NumberStyles.None,
            CultureInfo.InvariantCulture);
        var start = forecastStart < 0 ? 0 : Math.Max(0, forecastStart - 1);
        var end = forecastStart < 0
            ? summary.Forecast.Count
            : Math.Min(summary.Forecast.Count, forecastStart + horizonMonths + 1);
        var points = summary.Forecast.Skip(start).Take(end - start).ToArray();

        return ApiOkCached(new DashboardForecastDto(horizon, points));
    }

    [HttpGet("stock-status")]
    [ResponseCache(Duration = 30, Location = ResponseCacheLocation.Client, VaryByHeader = "Accept")]
    public async Task<IActionResult> GetStockStatus(CancellationToken cancellationToken)
    {
        var summary = await _dashboardService.GetSummaryAsync(cancellationToken);
        return ApiOkCached(summary.StockStatus);
    }

    [HttpGet("urgent-items")]
    [ResponseCache(Duration = 30, Location = ResponseCacheLocation.Client, VaryByHeader = "Accept")]
    public async Task<IActionResult> GetUrgentItems(
        CancellationToken cancellationToken,
        [FromQuery]
        [Range(1, 50)]
        int limit = 4)
    {
        var summary = await _dashboardService.GetSummaryAsync(cancellationToken);
        return ApiOkCached(summary.UrgentItems.Take(limit).ToArray());
    }

    [HttpGet("top-products")]
    [ResponseCache(Duration = 30, Location = ResponseCacheLocation.Client, VaryByHeader = "Accept")]
    public async Task<IActionResult> GetTopProducts(
        CancellationToken cancellationToken,
        [FromQuery]
        [RegularExpression("^30d$")]
        string period = "30d",
        [FromQuery]
        [Range(1, 50)]
        int limit = 5)
    {
        var summary = await _dashboardService.GetSummaryAsync(cancellationToken);
        return ApiOkCached(summary.TopProducts.Take(limit).ToArray());
    }

    [HttpGet("ai-insights")]
    [ResponseCache(Duration = 30, Location = ResponseCacheLocation.Client, VaryByHeader = "Accept")]
    public async Task<IActionResult> GetAiInsights(CancellationToken cancellationToken)
    {
        var summary = await _dashboardService.GetSummaryAsync(cancellationToken);
        return ApiOkCached(summary.AiInsights);
    }
}
