using BAPStockManagement.Constants;
using BAPStockManagement.Models;
using BAPStockManagement.ViewModels.Home;
using Microsoft.EntityFrameworkCore;

namespace BAPStockManagement.Services;

public sealed class StockPredictionService
{
    private readonly BAPStockContext _context;
    private readonly SaleBillService _saleBillService;

    public StockPredictionService(BAPStockContext context, SaleBillService saleBillService)
    {
        _context = context;
        _saleBillService = saleBillService;
    }

    public async Task<IReadOnlyList<StockPredictionViewModel>> GetPredictionsAsync(
        IReadOnlyCollection<Product> products,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        if (products.Count == 0)
        {
            return [];
        }

        var productIds = products.Select(product => product.ProductId).ToArray();
        var historyStart = today.AddDays(-89);
        var saleOutTypeId = await _saleBillService.GetPrimarySaleOutTypeIdAsync();
        var transactions = await _context.StockTransactions
            .AsNoTracking()
            .Where(transaction =>
                productIds.Contains(transaction.Variant.ProductId) &&
                transaction.Variant.IsActive &&
                transaction.TransactionTypeId == saleOutTypeId.GetValueOrDefault() &&
                transaction.TxnDate >= historyStart &&
                transaction.TxnDate <= today)
            .Select(transaction => new
            {
                transaction.Variant.ProductId,
                transaction.TxnDate,
                transaction.QtyPieces,
                transaction.QtyCases
            })
            .ToListAsync(cancellationToken);

        var salesByProduct = transactions
            .GroupBy(transaction => transaction.ProductId)
            .ToDictionary(
                group => group.Key,
                group => group.ToList());

        return products.Select(product =>
        {
            salesByProduct.TryGetValue(product.ProductId, out var productSales);
            var usesCases = ProductUnits.IsCaseUnit(product.Unit);
            var salesHistory = (productSales ?? [])
                .Select(transaction => new StockSalesEntry(
                    transaction.TxnDate,
                    usesCases ? transaction.QtyCases : transaction.QtyPieces))
                .ToList();
            var currentStock = product.ProductVariants
                .Where(variant => variant.IsActive)
                .Sum(variant => usesCases
                    ? variant.StockBalance?.QtyCases ?? 0
                    : variant.StockBalance?.QtyPieces ?? 0);

            return StockPredictionCalculator.Calculate(
                product.ProductId,
                product.Sku,
                product.ProductName,
                ProductUnits.Normalize(product.Unit),
                currentStock,
                salesHistory,
                today);
        }).ToList();
    }
}

public sealed record StockSalesEntry(DateOnly Date, int Quantity);

public static class StockPredictionCalculator
{
    public static StockPredictionViewModel Calculate(
        int productId,
        string sku,
        string productName,
        string unit,
        int currentStock,
        IReadOnlyCollection<StockSalesEntry> sales,
        DateOnly today)
    {
        var dailySales = sales
            .Where(sale => sale.Date >= today.AddDays(-89) && sale.Date <= today && sale.Quantity > 0)
            .GroupBy(sale => sale.Date)
            .ToDictionary(group => group.Key, group => group.Sum(sale => sale.Quantity));

        var sales30Days = SumSales(dailySales, today.AddDays(-29), today);
        var sales60Days = SumSales(dailySales, today.AddDays(-59), today);
        var sales90Days = SumSales(dailySales, today.AddDays(-89), today);
        var previous30Days = SumSales(dailySales, today.AddDays(-59), today.AddDays(-30));
        var windowDays = sales30Days > 0 ? 30 : sales60Days > 0 ? 60 : sales90Days > 0 ? 90 : (int?)null;
        var trend = GetTrend(sales30Days, previous30Days, sales90Days > 0);
        var history = Enumerable.Range(0, 30)
            .Select(offset =>
            {
                var date = today.AddDays(offset - 29);
                dailySales.TryGetValue(date, out var quantity);
                return new StockPredictionChartPoint
                {
                    Label = date.ToString("dd/MM"),
                    Quantity = quantity
                };
            })
            .ToList();

        if (!windowDays.HasValue)
        {
            var isOutOfStock = currentStock <= 0;
            return new StockPredictionViewModel
            {
                ProductId = productId,
                Sku = sku,
                ProductName = productName,
                Unit = unit,
                CurrentStock = currentStock,
                SalesTrend = StockSalesTrend.InsufficientData,
                RiskLevel = isOutOfStock ? StockRiskLevel.Critical : StockRiskLevel.InsufficientData,
                DaysUntilStockout = isOutOfStock ? 0 : null,
                EstimatedStockoutDate = isOutOfStock ? today : null,
                Recommendation = isOutOfStock
                    ? "สินค้าหมดสต็อกแล้ว ควรตรวจสอบและเติมสินค้าโดยเร็ว"
                    : "ข้อมูลไม่เพียงพอสำหรับการคาดการณ์",
                SalesHistory = history
            };
        }

        var selectedSales = windowDays.Value switch
        {
            30 => sales30Days,
            60 => sales60Days,
            _ => sales90Days
        };
        var averageDailySales = (decimal)selectedSales / windowDays.Value;
        var daysUntilStockout = currentStock <= 0 ? 0 : currentStock / averageDailySales;
        var roundedDaysUntilStockout = decimal.Ceiling(daysUntilStockout);
        var maxForecastDays = DateOnly.MaxValue.DayNumber - today.DayNumber;
        var estimatedDate = roundedDaysUntilStockout <= maxForecastDays
            ? today.AddDays(decimal.ToInt32(roundedDaysUntilStockout))
            : (DateOnly?)null;
        var riskLevel = daysUntilStockout <= 3
            ? StockRiskLevel.Critical
            : daysUntilStockout <= 7
                ? StockRiskLevel.Warning
                : StockRiskLevel.Normal;
        var recommendation = riskLevel switch
        {
            StockRiskLevel.Critical => "คาดว่าสินค้าจะหมดภายใน 3 วัน ควรเร่งตรวจสอบและสั่งซื้อเพิ่มเติม",
            StockRiskLevel.Warning => "สินค้ามีแนวโน้มหมดภายใน 7 วัน ควรเตรียมสั่งซื้อเพิ่มเติม",
            _ when trend == StockSalesTrend.Increasing => "ยอดจ่ายออกมีแนวโน้มเพิ่มขึ้น ควรติดตามสต็อกและวางแผนสั่งซื้อล่วงหน้า",
            _ => "สต็อกยังเพียงพอตามอัตราจ่ายออกล่าสุด ควรติดตามแนวโน้มต่อเนื่อง"
        };

        if (currentStock <= 0)
        {
            recommendation = "สินค้าหมดสต็อกแล้ว ควรตรวจสอบและเติมสินค้าโดยเร็ว";
        }

        return new StockPredictionViewModel
        {
            ProductId = productId,
            Sku = sku,
            ProductName = productName,
            Unit = unit,
            CurrentStock = currentStock,
            AverageDailySales = averageDailySales,
            DaysUntilStockout = daysUntilStockout,
            EstimatedStockoutDate = estimatedDate,
            SalesTrend = trend,
            RiskLevel = riskLevel,
            AnalysisWindowDays = windowDays,
            Recommendation = recommendation,
            SalesHistory = history
        };
    }

    private static int SumSales(
        IReadOnlyDictionary<DateOnly, int> dailySales,
        DateOnly start,
        DateOnly end)
        => dailySales
            .Where(entry => entry.Key >= start && entry.Key <= end)
            .Sum(entry => entry.Value);

    private static StockSalesTrend GetTrend(int recentSales, int previousSales, bool hasHistory)
    {
        if (!hasHistory)
        {
            return StockSalesTrend.InsufficientData;
        }

        if (recentSales > previousSales)
        {
            return StockSalesTrend.Increasing;
        }

        if (recentSales < previousSales)
        {
            return StockSalesTrend.Decreasing;
        }

        return StockSalesTrend.Stable;
    }
}
