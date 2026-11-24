using System.Globalization;
using BAPStockManagement.Services.Dashboard;
using BAPStockManagement.ViewModels.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BAPStockManagement.Controllers;

[Authorize]
[Route("[controller]")]
public sealed class DashboardController : Controller
{
    private readonly IDashboardService _dashboardService;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public DashboardController(
        IDashboardService dashboardService,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _dashboardService = dashboardService;
        _configuration = configuration;
        _environment = environment;
    }

    [HttpGet("")]
    [AllowAnonymous]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var isDevelopmentPreview = DashboardPreviewMode.IsEnabled(
                _configuration,
                _environment)
            && DashboardPreviewMode.IsLoopback(HttpContext);
        if (User.Identity?.IsAuthenticated != true && !isDevelopmentPreview)
        {
            return Challenge();
        }

        var summary = await _dashboardService.GetSummaryAsync(cancellationToken);
        var reportDate = DateOnly.FromDateTime(DateTime.Today);
        var thaiCulture = (CultureInfo)CultureInfo.GetCultureInfo("th-TH").Clone();
        thaiCulture.DateTimeFormat.Calendar = new GregorianCalendar();
        var reportDateLabel = reportDate
            .ToDateTime(TimeOnly.MinValue)
            .ToString("'วัน'dddd'ที่' d MMMM yyyy", thaiCulture);

        var model = new DashboardPageViewModel(
            summary,
            User.Identity?.Name ?? "Kunakorn K.",
            "IT Department",
            reportDate,
            reportDateLabel);

        ViewData["DashboardPreview"] = isDevelopmentPreview;
        return View(model);
    }
}
