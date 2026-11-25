using System.Text.Json;
using System.Text.RegularExpressions;
using BAPStockManagement.Constants;
using BAPStockManagement.Models;
using BAPStockManagement.ViewModels.Home;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BAPStockManagement.Services;

public sealed partial class SmartStockDashboardService(
    BAPStockContext context,
    SaleBillService saleBillService,
    SmartStockForecastRunner runner,
    IWebHostEnvironment environment,
    IMemoryCache cache,
    IConfiguration configuration,
    ILogger<SmartStockDashboardService> logger)
{
    private const int BulkThreshold = 100;
    private const int DisplayWeeks = 12;
    private const int HistoryWeeks = 208;
    private static readonly IReadOnlyDictionary<string, string> SegmentLabels = new Dictionary<string, string>
    {
        ["bicycle_12"] = "จักรยาน 12 นิ้ว",
        ["bicycle_16"] = "จักรยาน 16 นิ้ว",
        ["bicycle_18"] = "จักรยาน 18 นิ้ว",
        ["bicycle_20"] = "จักรยาน 20 นิ้ว",
        ["bicycle_24"] = "จักรยาน 24 นิ้ว",
        ["bicycle_other"] = "จักรยานขนาดอื่น",
        ["ride_on"] = "รถเด็กนั่ง",
        ["pool"] = "สระน้ำ",
        ["tyre"] = "ยาง"
    };

    public async Task<SmartStockDashboardViewModel> GetDashboardDataAsync(
        IReadOnlyCollection<Product> activeProducts, DateOnly today, CancellationToken cancellationToken)
    {
        var forecastWeek = StartOfWeek(today);
        var saleType = await saleBillService.GetPrimarySaleOutTypeIdAsync();
        if (saleType == null)
        {
            return Empty(activeProducts.Count, forecastWeek, "ยังไม่พบประเภทรายการขาย");
        }

        var historyLimit = forecastWeek.AddDays(-7 * HistoryWeeks);
        var rows = await context.StockTransactions.AsNoTracking()
            .Where(row => row.TransactionTypeId == saleType && row.TxnDate >= historyLimit &&
                          row.TxnDate < forecastWeek && (row.QtyPieces > 0 || row.QtyCases > 0))
            .Select(row => new SaleLine(row.Variant.ProductId, row.TxnDate, row.QtyPieces, row.QtyCases))
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return Empty(activeProducts.Count, forecastWeek, "ยังไม่มียอดขายจากฐานข้อมูลในสัปดาห์ที่ปิดแล้ว");
        }

        var firstSale = rows.Min(row => row.Date);
        var lastSale = rows.Max(row => row.Date);
        var firstCompleteWeek = firstSale.DayOfWeek == DayOfWeek.Monday
            ? firstSale
            : StartOfWeek(firstSale).AddDays(7);
        var lastCompleteWeek = forecastWeek.AddDays(-7);
        if (firstCompleteWeek > lastCompleteWeek)
        {
            return Empty(activeProducts.Count, forecastWeek, "ยังมีข้อมูลขายไม่ครบหนึ่งสัปดาห์");
        }

        var archive = ReadAsset<ForecastArchive>("verified_history.json");
        var skuMap = ReadAsset<SkuMap>("sku_segments.json");
        var catalog = await context.Products.AsNoTracking()
            .Select(product => new CatalogItem(product.ProductId, product.Sku, product.ProductName,
                product.Category.CategoryName, product.Unit))
            .ToListAsync(cancellationToken);
        var products = catalog.ToDictionary(product => product.Id, product => new
        {
            Segment = MatchSegment(product, skuMap),
            UsesCases = ProductUnits.IsCaseUnit(product.Unit)
        });

        var weekly = new Dictionary<(DateOnly Week, string Segment), long>();
        var recentByProduct = new Dictionary<int, long>();
        var allocationStart = forecastWeek.AddDays(-7 * DisplayWeeks);
        foreach (var row in rows)
        {
            if (!products.TryGetValue(row.ProductId, out var product))
            {
                continue;
            }
            var quantity = product.UsesCases ? row.Cases : row.Pieces;
            if (product.Segment == null || quantity is <= 0 or > BulkThreshold || row.Date < firstCompleteWeek)
            {
                continue;
            }

            var key = (StartOfWeek(row.Date), product.Segment);
            weekly[key] = weekly.GetValueOrDefault(key) + quantity;
            if (row.Date >= allocationStart)
            {
                recentByProduct[row.ProductId] = recentByProduct.GetValueOrDefault(row.ProductId) + quantity;
            }
        }

        var actualThrough = lastSale >= lastCompleteWeek
            ? lastCompleteWeek
            : StartOfWeek(lastSale);
        var actualFrom = allocationStart > firstCompleteWeek ? allocationStart : firstCompleteWeek;
        IReadOnlyDictionary<string, SmartStockForecast> predictions = new Dictionary<string, SmartStockForecast>();
        string? status = null;
        if (actualThrough < lastCompleteWeek)
        {
            status = "ยังไม่พบยอดขายในสัปดาห์ที่ปิดล่าสุด จึงยังไม่อัปเดตผลพยากรณ์";
        }
        else
        {
            var history = BuildForecastInput(archive, firstCompleteWeek, lastCompleteWeek,
                forecastWeek, historyLimit, weekly);
            if (history.History.Values.All(values => values.Count(value => value.HasValue) < 52))
            {
                status = "ยังมีประวัติยอดขายไม่พอสำหรับพยากรณ์";
            }
            else
            {
                predictions = await runner.PredictAsync(history, cancellationToken);
                if (predictions.Count == 0)
                {
                    status = "ยังไม่มีผลพยากรณ์ ตรวจสอบ Python และไฟล์โมเดลของระบบ";
                }
                else if (predictions.Count < SegmentLabels.Count)
                {
                    status = $"มีผลพยากรณ์ {predictions.Count} จาก {SegmentLabels.Count} กลุ่ม";
                }
            }
        }

        var salesPoints = new List<SmartStockSalesPoint>();
        foreach (var segment in SegmentLabels.Keys)
        {
            for (var week = actualFrom; week <= actualThrough; week = week.AddDays(7))
            {
                salesPoints.Add(new(week, segment, weekly.GetValueOrDefault((week, segment)), null));
            }

            if (predictions.TryGetValue(segment, out var prediction))
            {
                salesPoints.Add(new(forecastWeek, segment, null, prediction.Quantity));
            }
        }

        var activeById = activeProducts.ToDictionary(product => product.ProductId);
        var analyses = new List<SmartStockAnalysis>();
        var safetyStock = Math.Max(0, configuration.GetValue("SmartStockForecast:SafetyStock", SmartStockDemoCalculator.DefaultSafetyStock));
        foreach (var (segment, prediction) in predictions)
        {
            var recentSales = recentByProduct.Where(sale => products[sale.Key].Segment == segment).ToArray();
            if (recentSales.Length == 0 || !double.IsFinite(prediction.Quantity) ||
                prediction.Quantity is < 0 or > int.MaxValue)
            {
                continue;
            }

            foreach (var allocation in AllocateDemand(recentSales, prediction.Quantity))
            {
                if (!activeById.TryGetValue(allocation.ProductId, out var product))
                {
                    continue;
                }

                var usesCases = products[product.ProductId].UsesCases;
                var stock = product.ProductVariants.Where(variant => variant.IsActive).Sum(variant => usesCases
                    ? variant.StockBalance?.QtyCases ?? 0
                    : variant.StockBalance?.QtyPieces ?? 0);
                var (risk, purchase) = SmartStockDemoCalculator.Analyze(new(
                    product.ProductId, product.ProductName, segment, stock, allocation.Demand,
                    allocation.Demand > 0 ? safetyStock : 0));
                analyses.Add(new(product.Sku, ProductUnits.Normalize(product.Unit), SegmentLabels[segment],
                    prediction.Model, allocation.Share, risk, purchase));
            }
        }

        var segments = SegmentLabels.Select(pair =>
        {
            predictions.TryGetValue(pair.Key, out var prediction);
            return new SmartStockSegmentSummary(pair.Key, pair.Value,
                weekly.GetValueOrDefault((actualThrough, pair.Key)), prediction?.Quantity, prediction?.Model);
        }).ToArray();

        return new SmartStockDashboardViewModel
        {
            ForecastWeek = forecastWeek,
            ActualsFrom = actualFrom <= actualThrough ? actualFrom : null,
            ActualsThrough = actualThrough,
            ActualSales = actualFrom <= actualThrough ? salesPoints.Sum(point => point.Actual ?? 0) : null,
            ForecastSales = predictions.Count > 0 ? predictions.Values.Sum(prediction => prediction.Quantity) : null,
            ProductCount = activeProducts.Count,
            ForecastStatus = status,
            WeeklySales = salesPoints,
            Segments = segments,
            Analyses = analyses.OrderByDescending(item => item.Risk.RiskScore)
                .ThenByDescending(item => item.Purchase.SuggestedQuantity)
                .ThenBy(item => item.Sku, StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    private static SmartStockForecastInput BuildForecastInput(ForecastArchive? archive, DateOnly firstCompleteWeek,
        DateOnly lastCompleteWeek, DateOnly forecastWeek, DateOnly historyLimit,
        Dictionary<(DateOnly Week, string Segment), long> weekly)
    {
        var start = archive != null && archive.StartWeek < firstCompleteWeek ? archive.StartWeek : firstCompleteWeek;
        if (start < historyLimit)
        {
            start = historyLimit;
        }
        var count = (forecastWeek.DayNumber - start.DayNumber) / 7;
        var history = new Dictionary<string, double?[]>();
        foreach (var segment in SegmentLabels.Keys)
        {
            var values = new double?[count];
            for (var index = 0; index < count; index++)
            {
                var week = start.AddDays(index * 7);
                if (week >= firstCompleteWeek && week <= lastCompleteWeek)
                {
                    values[index] = weekly.GetValueOrDefault((week, segment));
                }
                else if (archive != null && week >= archive.StartWeek && week <= archive.DataThrough &&
                         archive.History.TryGetValue(segment, out var archiveValues))
                {
                    var archiveIndex = (week.DayNumber - archive.StartWeek.DayNumber) / 7;
                    values[index] = archiveIndex < archiveValues.Length ? archiveValues[archiveIndex] : null;
                }
            }
            history[segment] = values;
        }
        return new(start, forecastWeek, history);
    }

    private static IEnumerable<(int ProductId, int Demand, decimal Share)> AllocateDemand(
        KeyValuePair<int, long>[] sales, double rawDemand)
    {
        var demand = (int)Math.Round(rawDemand, MidpointRounding.AwayFromZero);
        var total = sales.Sum(sale => sale.Value);
        var shares = sales.Select(sale =>
        {
            var share = (decimal)sale.Value / total;
            var exact = share * demand;
            return (ProductId: sale.Key, Share: share, Whole: (int)exact, Fraction: exact % 1);
        }).OrderByDescending(item => item.Fraction).ThenBy(item => item.ProductId).ToArray();
        var remainder = demand - shares.Sum(item => item.Whole);
        return shares.Select((item, index) => (item.ProductId, item.Whole + (index < remainder ? 1 : 0), item.Share));
    }

    private static SmartStockDashboardViewModel Empty(int count, DateOnly forecastWeek, string status) => new()
    {
        ProductCount = count,
        ForecastWeek = forecastWeek,
        ForecastStatus = status,
        Segments = SegmentLabels.Select(pair => new SmartStockSegmentSummary(pair.Key, pair.Value, null, null, null)).ToArray()
    };

    private T? ReadAsset<T>(string fileName) where T : class
    {
        return cache.GetOrCreate(fileName, entry =>
        {
            var path = Path.Combine(environment.ContentRootPath, "Forecasting", fileName);
            try
            {
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path), SmartStockForecastRunner.JsonOptions);
            }
            catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException)
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                logger.LogWarning(error, "Unable to read Smart Stock asset {FileName}", fileName);
                return null;
            }
        });
    }

    private static DateOnly StartOfWeek(DateOnly date) => date.AddDays(-((int)date.DayOfWeek + 6) % 7);

    private static string? MatchSegment(CatalogItem product, SkuMap? skuMap)
    {
        var sku = product.Sku.Trim().ToUpperInvariant();
        var category = product.Category;
        if (skuMap != null && skuMap.Segments.TryGetValue(sku, out var mapped))
        {
            return mapped;
        }

        if (category.Contains("อะไหล่", StringComparison.Ordinal))
        {
            return null;
        }
        if (category.Contains("ยาง", StringComparison.Ordinal))
        {
            return "tyre";
        }
        if (category.Contains("สระน้ำ", StringComparison.Ordinal))
        {
            return "pool";
        }
        if (category.Contains("รถหัดเดิน", StringComparison.Ordinal) ||
            category.Contains("รถเด็ก", StringComparison.Ordinal))
        {
            return "ride_on";
        }
        if (sku.StartsWith("BLB-", StringComparison.Ordinal) ||
            category.Contains("จักรยาน", StringComparison.Ordinal) ||
            category.Contains("จักรบยาน", StringComparison.Ordinal))
        {
            var size = BicycleSize().Match(product.Name);
            return size.Success ? $"bicycle_{size.Groups[1].Value}" : "bicycle_other";
        }
        return null;
    }

    [GeneratedRegex("""(?<!\d)(12|16|18|20|24)\s*(?:["″“”]|นิ้ว|in\b)""", RegexOptions.IgnoreCase)]
    private static partial Regex BicycleSize();

    private sealed record SaleLine(int ProductId, DateOnly Date, int Pieces, int Cases);
    private sealed record CatalogItem(int Id, string Sku, string Name, string Category, string Unit);
    private sealed record ForecastArchive(DateOnly StartWeek, DateOnly DataThrough, Dictionary<string, double?[]> History);
    private sealed record SkuMap(Dictionary<string, string> Segments);
}
