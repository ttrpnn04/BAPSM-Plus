using System.Diagnostics;
using BAPStockManagement.Constants;
using BAPStockManagement.ViewModels.Home;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BAPStockManagement.Models;

namespace BAPStockManagement.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly BAPStockContext _context;

    public HomeController(BAPStockContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var products = await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.ProductVariants)
                .ThenInclude(v => v.StockBalance)
            .Where(p => p.IsActive && p.Category.IsActive)
            .ToListAsync();

        var productStockRows = products
            .Select(p =>
            {
                var activeVariants = p.ProductVariants.Where(v => v.IsActive).ToList();

                return new
                {
                    ProductId = p.ProductId,
                    CategoryName = p.Category.CategoryName,
                    Sku = p.Sku,
                    ProductName = p.ProductName,
                    Unit = ProductUnits.Normalize(p.Unit),
                    VariantCount = activeVariants.Count,
                    QtyPieces = activeVariants.Sum(v => v.StockBalance?.QtyPieces ?? 0),
                    QtyCases = activeVariants.Sum(v => v.StockBalance?.QtyCases ?? 0)
                };
            })
            .ToList();
        var productStockById = productStockRows.ToDictionary(r => r.ProductId);

        var lowStockItems = productStockRows
            .Where(r => StockThresholds.IsLowStock(r.QtyPieces, r.QtyCases))
            .OrderBy(r => r.QtyPieces > 0 ? r.QtyPieces : r.QtyCases)
            .ThenBy(r => r.ProductName)
            .Take(8)
            .Select(r => new StockAlertItem
            {
                CategoryName = r.CategoryName,
                Sku = r.Sku,
                ProductName = r.ProductName,
                Unit = r.Unit,
                QtyPieces = r.QtyPieces,
                QtyCases = r.QtyCases
            })
            .ToList();

        var outOfStockItems = productStockRows
            .Where(r => StockThresholds.IsOutOfStock(r.QtyPieces, r.QtyCases))
            .OrderBy(r => r.ProductName)
            .Take(8)
            .Select(r => new StockAlertItem
            {
                CategoryName = r.CategoryName,
                Sku = r.Sku,
                ProductName = r.ProductName,
                Unit = r.Unit,
                QtyPieces = r.QtyPieces,
                QtyCases = r.QtyCases
            })
            .ToList();

        var stockDistributionChartItems = productStockRows
            .GroupBy(r => r.CategoryName)
            .Select(g => new CategoryStockChartItem
            {
                CategoryName = g.Key,
                QtyPieces = g.Sum(r => r.QtyPieces)
            })
            .OrderByDescending(i => i.QtyPieces)
            .ThenBy(i => i.CategoryName)
            .ToList();
        var categoryStockChartItems = stockDistributionChartItems.Take(6).ToList();

        var dailyMovements = await _context.StockTransactions
            .AsNoTracking()
            .Include(t => t.TransactionType)
            .Where(t => t.TxnDate >= monthStart && t.TxnDate <= monthEnd)
            .GroupBy(t => t.TxnDate)
            .Select(g => new
            {
                TxnDate = g.Key,
                InPieces = g
                    .Where(t => t.TransactionType.Direction > 0)
                    .Sum(t => (int?)t.QtyPieces) ?? 0,
                OutPieces = g
                    .Where(t => t.TransactionType.Direction < 0)
                    .Sum(t => (int?)t.QtyPieces) ?? 0
            })
            .ToListAsync();

        var movementByDate = dailyMovements.ToDictionary(i => i.TxnDate);
        var dailyMovementChartItems = Enumerable.Range(0, today.Day)
            .Select(offset =>
            {
                var date = monthStart.AddDays(offset);
                movementByDate.TryGetValue(date, out var movement);

                return new DailyMovementChartItem
                {
                    Label = date.Day.ToString("00"),
                    InPieces = movement?.InPieces ?? 0,
                    OutPieces = movement?.OutPieces ?? 0
                };
            })
            .ToList();

        var chartStart = monthStart.AddMonths(-5);
        var monthlyMovements = await _context.StockTransactions
            .AsNoTracking()
            .Where(t => t.TxnDate >= chartStart && t.TxnDate <= today)
            .GroupBy(t => new { t.TxnDate.Year, t.TxnDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                InPieces = g
                    .Where(t => t.TransactionType.Direction > 0)
                    .Sum(t => (int?)t.QtyPieces) ?? 0,
                OutPieces = g
                    .Where(t => t.TransactionType.Direction < 0)
                    .Sum(t => (int?)t.QtyPieces) ?? 0
            })
            .ToListAsync();

        var movementByMonth = monthlyMovements.ToDictionary(i => (i.Year, i.Month));
        var thaiMonthLabels = new[] { "ม.ค.", "ก.พ.", "มี.ค.", "เม.ย.", "พ.ค.", "มิ.ย.", "ก.ค.", "ส.ค.", "ก.ย.", "ต.ค.", "พ.ย.", "ธ.ค." };
        var monthlyMovementChartItems = Enumerable.Range(0, 6)
            .Select(offset =>
            {
                var date = monthStart.AddMonths(offset - 5);
                movementByMonth.TryGetValue((date.Year, date.Month), out var movement);

                return new MonthlyMovementChartItem
                {
                    Label = thaiMonthLabels[date.Month - 1],
                    InPieces = movement?.InPieces ?? 0,
                    OutPieces = movement?.OutPieces ?? 0
                };
            })
            .ToList();

        var recentUsageStart = today.AddDays(-29);
        var previousUsageStart = recentUsageStart.AddDays(-30);
        var productUsageRows = await _context.StockTransactions
            .AsNoTracking()
            .Where(t =>
                t.TxnDate >= previousUsageStart &&
                t.TxnDate <= today &&
                t.TransactionType.Direction < 0 &&
                t.Variant.IsActive &&
                t.Variant.Product.IsActive &&
                t.Variant.Product.Category.IsActive)
            .GroupBy(t => new
            {
                t.Variant.ProductId,
                t.Variant.Product.Sku,
                t.Variant.Product.ProductName,
                t.Variant.Product.Unit
            })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.Sku,
                g.Key.ProductName,
                g.Key.Unit,
                CurrentPieces = g
                    .Where(t => t.TxnDate >= recentUsageStart)
                    .Sum(t => (int?)t.QtyPieces) ?? 0,
                CurrentCases = g
                    .Where(t => t.TxnDate >= recentUsageStart)
                    .Sum(t => (int?)t.QtyCases) ?? 0,
                PreviousPieces = g
                    .Where(t => t.TxnDate < recentUsageStart)
                    .Sum(t => (int?)t.QtyPieces) ?? 0,
                PreviousCases = g
                    .Where(t => t.TxnDate < recentUsageStart)
                    .Sum(t => (int?)t.QtyCases) ?? 0
            })
            .ToListAsync();

        var topUsageRows = productUsageRows
            .Select(row => new
            {
                Row = row,
                CurrentQuantity = ProductUnits.IsCaseUnit(row.Unit) ? row.CurrentCases : row.CurrentPieces,
                PreviousQuantity = ProductUnits.IsCaseUnit(row.Unit) ? row.PreviousCases : row.PreviousPieces
            })
            .Where(row => row.CurrentQuantity > 0)
            .OrderByDescending(row => row.CurrentQuantity)
            .ThenBy(row => row.Row.ProductName)
            .Take(5)
            .ToList();
        var topProductIds = topUsageRows.Select(row => row.Row.ProductId).ToArray();

        var topProductDailyUsage = await _context.StockTransactions
            .AsNoTracking()
            .Where(t =>
                topProductIds.Contains(t.Variant.ProductId) &&
                t.TxnDate >= recentUsageStart &&
                t.TxnDate <= today &&
                t.TransactionType.Direction < 0 &&
                t.Variant.IsActive)
            .GroupBy(t => new { t.Variant.ProductId, t.TxnDate })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.TxnDate,
                OutPieces = g.Sum(t => (int?)t.QtyPieces) ?? 0,
                OutCases = g.Sum(t => (int?)t.QtyCases) ?? 0
            })
            .ToListAsync();

        var topUsedProducts = topUsageRows
            .Select(row =>
            {
                var trendBuckets = new int[6];
                foreach (var dailyUsage in topProductDailyUsage.Where(item => item.ProductId == row.Row.ProductId))
                {
                    var bucketIndex = Math.Min((dailyUsage.TxnDate.DayNumber - recentUsageStart.DayNumber) / 5, 5);
                    var quantity = ProductUnits.IsCaseUnit(row.Row.Unit) ? dailyUsage.OutCases : dailyUsage.OutPieces;
                    trendBuckets[bucketIndex] += quantity;
                }

                var changePercent = row.PreviousQuantity == 0
                    ? (decimal?)null
                    : Math.Round((row.CurrentQuantity - row.PreviousQuantity) * 100m / row.PreviousQuantity);

                return new TopUsedProductItem
                {
                    Sku = row.Row.Sku,
                    ProductName = row.Row.ProductName,
                    Unit = ProductUnits.Normalize(row.Row.Unit),
                    UsedQuantity = row.CurrentQuantity,
                    CurrentStock = ProductUnits.IsCaseUnit(row.Row.Unit)
                        ? productStockById[row.Row.ProductId].QtyCases
                        : productStockById[row.Row.ProductId].QtyPieces,
                    ChangePercent = changePercent,
                    HasPreviousPeriodUsage = row.PreviousQuantity > 0,
                    TrendBuckets = trendBuckets
                };
            })
            .ToList();

        var monthTransactions = await _context.StockTransactions
            .AsNoTracking()
            .Include(t => t.TransactionType)
            .Where(t => t.TxnDate >= monthStart && t.TxnDate <= monthEnd)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                InPieces = g
                    .Where(t => t.TransactionType.Direction > 0)
                    .Sum(t => (int?)t.QtyPieces) ?? 0,
                OutPieces = g
                    .Where(t => t.TransactionType.Direction < 0)
                    .Sum(t => (int?)t.QtyPieces) ?? 0
            })
            .FirstOrDefaultAsync();

        var lastTransactionAt = await _context.StockTransactions
            .AsNoTracking()
            .Select(t => (DateTime?)t.CreatedAt)
            .MaxAsync();

        var outOfStockProductCount = productStockRows.Count(r => StockThresholds.IsOutOfStock(r.QtyPieces, r.QtyCases));
        var lowStockProductCount = productStockRows.Count(r => StockThresholds.IsLowStock(r.QtyPieces, r.QtyCases));
        var stockInsights = new List<string>();
        if (outOfStockProductCount > 0)
        {
            stockInsights.Add($"มีสินค้า {outOfStockProductCount:N0} รายการที่หมดสต็อก");
        }

        if (lowStockProductCount > 0)
        {
            stockInsights.Add($"พบสินค้าใกล้หมด {lowStockProductCount:N0} รายการ ตามเกณฑ์เตือนของระบบ");
        }

        if (topUsedProducts.Count > 0)
        {
            var topUsedProduct = topUsedProducts[0];
            var usageChangeSummary = topUsedProduct.HasPreviousPeriodUsage
                ? topUsedProduct.ChangePercent.GetValueOrDefault() > 0
                    ? $"เพิ่มขึ้น {topUsedProduct.ChangePercent.GetValueOrDefault():N0}%"
                    : topUsedProduct.ChangePercent.GetValueOrDefault() < 0
                        ? $"ลดลง {Math.Abs(topUsedProduct.ChangePercent.GetValueOrDefault()):N0}%"
                        : "ทรงตัว"
                : "ยังไม่มีข้อมูลเปรียบเทียบช่วงก่อน";
            stockInsights.Add(
                $"ยอดจ่ายออกสูงสุด 30 วัน: {topUsedProduct.ProductName} {topUsedProduct.UsedQuantity:N0} {topUsedProduct.Unit} ({usageChangeSummary})");
        }

        if (stockInsights.Count == 0)
        {
            stockInsights.Add("ยังไม่พบสินค้าใกล้หมดหรือหมดสต็อก และไม่มีรายการจ่ายออกในช่วง 30 วันที่ผ่านมา");
        }

        var stockInsightRecommendation = outOfStockProductCount + lowStockProductCount > 0
            ? "ควรตรวจสอบรายการเร่งด่วนก่อนวางแผนเติมสต็อก"
            : topUsedProducts.Count > 0
                ? "ใช้ยอดจ่ายออกย้อนหลัง 30 วันประกอบการวางแผนเติมสต็อก"
                : "บันทึกรายการจ่ายออกเพื่อเริ่มติดตามแนวโน้มสินค้า";

        return View(new HomeDashboardViewModel
        {
            ActiveProductCount = products.Count,
            ActiveCategoryCount = await _context.Categories.CountAsync(c => c.IsActive),
            ActiveVariantCount = productStockRows.Sum(r => r.VariantCount),
            TotalPieces = productStockRows.Sum(r => r.QtyPieces),
            TotalCases = productStockRows.Sum(r => r.QtyCases),
            OutOfStockProductCount = outOfStockProductCount,
            LowStockProductCount = lowStockProductCount,
            MonthInPieces = monthTransactions?.InPieces ?? 0,
            MonthOutPieces = monthTransactions?.OutPieces ?? 0,
            MonthTransactionCount = monthTransactions?.Count ?? 0,
            LastTransactionAt = lastTransactionAt,
            CategoryStockChartItems = categoryStockChartItems,
            StockDistributionChartItems = stockDistributionChartItems,
            DailyMovementChartItems = dailyMovementChartItems,
            MonthlyMovementChartItems = monthlyMovementChartItems,
            TopUsedProducts = topUsedProducts,
            StockInsights = stockInsights,
            StockInsightRecommendation = stockInsightRecommendation,
            LowStockItems = lowStockItems,
            OutOfStockItems = outOfStockItems
        });
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
