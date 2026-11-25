using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BAPStockManagement.Controllers;

[Authorize]
[Route("stock-analysis")]
public class StockAnalysisController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "วิเคราะห์สต็อกสินค้า";
        return View();
    }
}
