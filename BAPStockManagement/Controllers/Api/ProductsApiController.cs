using System.ComponentModel.DataAnnotations;
using BAPStockManagement.Services.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BAPStockManagement.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/v1/products")]
public sealed class ProductsApiController : ApiControllerBase
{
    private readonly IDashboardService _dashboardService;

    public ProductsApiController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery(Name = "q")]
        [Required]
        [StringLength(100, MinimumLength = 1)]
        string query,
        CancellationToken cancellationToken,
        [FromQuery]
        [Range(1, 50)]
        int limit = 10)
    {
        var results = await _dashboardService.SearchProductsAsync(
            query,
            limit,
            cancellationToken);
        return ApiOk(results);
    }
}
