using BAPStockManagement.ViewModels.Home;

namespace BAPStockManagement.Services;

public sealed record SmartStockInventoryInput(
    int ProductId,
    string ProductName,
    string Segment,
    int CurrentStock,
    int ForecastDemand,
    int SafetyStock);

public static class SmartStockDemoCalculator
{
    public const int DefaultSafetyStock = 5;

    public static (SmartStockDemoRiskItem Risk, SmartStockDemoPurchaseItem Purchase) Analyze(
        SmartStockInventoryInput input)
    {
        var estimatedRemaining = input.CurrentStock - input.ForecastDemand;

        var riskLevel = estimatedRemaining < -input.SafetyStock
            ? "High"
            : estimatedRemaining <= input.SafetyStock
                ? "Medium"
                : "Low";

        var riskScore = input.ForecastDemand == 0
            ? 0
            : (int)Math.Min(100, Math.Round(
                Math.Max(0, (input.ForecastDemand - input.CurrentStock + input.SafetyStock)
                    * 100d / (input.ForecastDemand + input.SafetyStock))));

        var recommendedQuantity = Math.Max(
            0,
            input.ForecastDemand + input.SafetyStock - input.CurrentStock);

        var priority = riskLevel == "High"
            ? "Urgent"
            : riskLevel == "Medium"
                ? "Medium"
                : "Low";

        var explanation = estimatedRemaining < 0
            ? "Current inventory may not be sufficient to cover the forecast demand. This product should be prioritized for replenishment."
            : estimatedRemaining <= input.SafetyStock
                ? "Forecast demand may leave less stock than the configured safety level. Prepare a replenishment suggestion."
                : "Current inventory is above forecast demand and the configured safety stock. Continue monitoring.";

        var risk = new SmartStockDemoRiskItem
        {
            ProductId = input.ProductId,
            ProductName = input.ProductName,
            Segment = input.Segment,
            CurrentStock = input.CurrentStock,
            ForecastDemand = input.ForecastDemand,
            SafetyStock = input.SafetyStock,
            EstimatedRemainingStock = estimatedRemaining,
            RiskScore = riskScore,
            RiskLevel = riskLevel,
            RiskReason = explanation,
            RecommendedAction = recommendedQuantity > 0
                ? $"Simulated recommendation: prepare to purchase {recommendedQuantity} units."
                : "No replenishment quantity is required at this stage."
        };

        var purchase = new SmartStockDemoPurchaseItem
        {
            ProductId = input.ProductId,
            ProductName = input.ProductName,
            CurrentStock = input.CurrentStock,
            ForecastDemand = input.ForecastDemand,
            SafetyStock = input.SafetyStock,
            EstimatedRemainingStock = estimatedRemaining,
            SuggestedQuantity = recommendedQuantity,
            Priority = priority,
            RecommendationReason = recommendedQuantity > 0
                ? $"Calculated as Forecast Demand ({input.ForecastDemand}) + Safety Stock ({input.SafetyStock}) - Current Stock ({input.CurrentStock})."
                : "Current stock already covers forecast demand and the configured safety stock."
        };

        return (risk, purchase);
    }
}

public interface ISmartStockDemoDataProvider
{
    SmartStockDemoDashboardViewModel GetDashboardData();
}

public sealed class SmartStockDemoDataService : ISmartStockDemoDataProvider
{
    public SmartStockDemoDashboardViewModel GetDashboardData()
    {
        var weeklySales = new[]
        {
            NewWeek("2026-09-14", "Bicycle", 380, 392),
            NewWeek("2026-09-21", "Bicycle", 398, 405),
            NewWeek("2026-09-28", "Bicycle", 410, 418),
            NewWeek("2026-10-05", "Bicycle", 428, 424),
            NewWeek("2026-10-12", "Bicycle", 415, 431),
            NewWeek("2026-10-19", "Bicycle", 438, 440),
            NewWeek("2026-10-26", "Bicycle", 420, 438),
            NewWeek("2026-11-02", "Bicycle", 448, 452),
            NewWeek("2026-11-09", "Bicycle", 475, 468),
            NewWeek("2026-11-16", "Bicycle", 462, 486),
            NewWeek("2026-11-23", "Bicycle", 492, 501),
            NewWeek("2026-11-30", "Bicycle", 510, 518),
            NewWeek("2026-09-14", "Ride-on", 272, 280),
            NewWeek("2026-09-21", "Ride-on", 286, 292),
            NewWeek("2026-09-28", "Ride-on", 295, 301),
            NewWeek("2026-10-05", "Ride-on", 304, 310),
            NewWeek("2026-10-12", "Ride-on", 298, 316),
            NewWeek("2026-10-19", "Ride-on", 318, 321),
            NewWeek("2026-10-26", "Ride-on", 310, 326),
            NewWeek("2026-11-02", "Ride-on", 332, 338),
            NewWeek("2026-11-09", "Ride-on", 348, 352),
            NewWeek("2026-11-16", "Ride-on", 365, 360),
            NewWeek("2026-11-23", "Ride-on", 378, 372),
            NewWeek("2026-11-30", "Ride-on", 390, 386),
            NewWeek("2026-09-14", "Tyre", 468, 476),
            NewWeek("2026-09-21", "Tyre", 482, 488),
            NewWeek("2026-09-28", "Tyre", 496, 502),
            NewWeek("2026-10-05", "Tyre", 510, 514),
            NewWeek("2026-10-12", "Tyre", 528, 526),
            NewWeek("2026-10-19", "Tyre", 516, 534),
            NewWeek("2026-10-26", "Tyre", 520, 508),
            NewWeek("2026-11-02", "Tyre", 545, 536),
            NewWeek("2026-11-09", "Tyre", 532, 548),
            NewWeek("2026-11-16", "Tyre", 560, 566),
            NewWeek("2026-11-23", "Tyre", 582, 574),
            NewWeek("2026-11-30", "Tyre", 605, 598),
            NewWeek("2026-09-14", "Pool", 210, 218),
            NewWeek("2026-09-21", "Pool", 226, 232),
            NewWeek("2026-09-28", "Pool", 238, 244),
            NewWeek("2026-10-05", "Pool", 248, 252),
            NewWeek("2026-10-12", "Pool", 236, 260),
            NewWeek("2026-10-19", "Pool", 252, 264),
            NewWeek("2026-10-26", "Pool", 245, 258),
            NewWeek("2026-11-02", "Pool", 268, 270),
            NewWeek("2026-11-09", "Pool", 284, 281),
            NewWeek("2026-11-16", "Pool", 275, 292),
            NewWeek("2026-11-23", "Pool", 296, 304),
            NewWeek("2026-11-30", "Pool", 318, 312)
        };

        var segmentSummaries = weeklySales
            .GroupBy(point => point.Segment)
            .Select(group => new SmartStockDemoSegmentSummary
            {
                Segment = group.Key,
                ActualSales = group.Sum(point => point.ActualSales),
                ForecastSales = group.Sum(point => point.ForecastSales),
                Variance = group.Sum(point => point.ForecastSales - point.ActualSales)
            })
            .OrderBy(summary => summary.Segment)
            .ToList();

        var inventoryInputs = new[]
        {
            new SmartStockInventoryInput(1001, "Mountain Bicycle 20\"", "Bicycle", 8, 45, 5),
            new SmartStockInventoryInput(2004, "Kids Ride-on Red", "Ride-on", 30, 35, 5),
            new SmartStockInventoryInput(3007, "All-season Tyre 12\"", "Tyre", 32, 48, 8),
            new SmartStockInventoryInput(4002, "Pool Filter Cartridge", "Pool", 35, 26, 5)
        };

        var analyses = inventoryInputs
            .Select(SmartStockDemoCalculator.Analyze)
            .ToList();
        var stockRisks = analyses.Select(item => item.Risk).ToList();
        var purchaseRecommendations = analyses.Select(item => item.Purchase).ToList();

        return new SmartStockDemoDashboardViewModel
        {
            ModelName = "SARIMA_1step",
            ActualSales = weeklySales.Sum(point => point.ActualSales),
            ForecastSales = weeklySales.Sum(point => point.ForecastSales),
            ProductsAtRisk = stockRisks.Count,
            WeeklySales = weeklySales,
            SegmentSummaries = segmentSummaries,
            StockRisks = stockRisks,
            PurchaseRecommendations = purchaseRecommendations,
            ModelMetrics = [
                new SmartStockDemoMetric { Name = "MAE", Value = "Not provided" },
                new SmartStockDemoMetric { Name = "RMSE", Value = "Not provided" },
                new SmartStockDemoMetric { Name = "WMAPE", Value = "Not provided" }
            ]
        };
    }

    private static SmartStockDemoWeeklyPoint NewWeek(
        string weekStart,
        string segment,
        int actualSales,
        int forecastSales)
        => new()
        {
            WeekStart = weekStart,
            Segment = segment,
            ActualSales = actualSales,
            ForecastSales = forecastSales
        };
}
