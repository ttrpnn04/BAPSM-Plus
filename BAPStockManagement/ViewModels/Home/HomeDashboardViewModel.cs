namespace BAPStockManagement.ViewModels.Home;

public class HomeDashboardViewModel
{
    public int ActiveProductCount { get; set; }

    public int ActiveCategoryCount { get; set; }

    public int ActiveVariantCount { get; set; }

    public int TotalPieces { get; set; }

    public int TotalCases { get; set; }

    public int OutOfStockProductCount { get; set; }

    public int LowStockProductCount { get; set; }

    public int MonthInPieces { get; set; }

    public int MonthOutPieces { get; set; }

    public int MonthTransactionCount { get; set; }

    public DateTime? LastTransactionAt { get; set; }

    public IReadOnlyList<CategoryStockChartItem> CategoryStockChartItems { get; set; } = [];

    public IReadOnlyList<CategoryStockChartItem> StockDistributionChartItems { get; set; } = [];

    public IReadOnlyList<DailyMovementChartItem> DailyMovementChartItems { get; set; } = [];

    public IReadOnlyList<MonthlyMovementChartItem> MonthlyMovementChartItems { get; set; } = [];

    public IReadOnlyList<TopUsedProductItem> TopUsedProducts { get; set; } = [];

    public IReadOnlyList<string> StockInsights { get; set; } = [];

    public string StockInsightRecommendation { get; set; } = string.Empty;

    public IReadOnlyList<StockAlertItem> LowStockItems { get; set; } = [];

    public IReadOnlyList<StockPredictionViewModel> StockPredictions { get; set; } = [];

    public IReadOnlyList<StockPredictionViewModel> StockActionItems { get; set; } = [];

    public IReadOnlyList<SmartRestockItemViewModel> SmartRestockItems { get; set; } = [];

    public int StockAnalysisCount { get; set; }

    public int CriticalPredictionCount { get; set; }

    public int StockoutWithinSevenDaysCount { get; set; }

    public int IncreasingSalesTrendCount { get; set; }

    public IReadOnlyList<StockAlertItem> OutOfStockItems { get; set; } = [];

    public SmartStockDemoDashboardViewModel SmartStockDemo { get; set; } = new();
}

public sealed class SmartStockDemoDashboardViewModel
{
    public string ModelName { get; set; } = "SARIMA_1step";

    public int ActualSales { get; set; }

    public int ForecastSales { get; set; }

    public int ProductsAtRisk { get; set; }

    public IReadOnlyList<SmartStockDemoWeeklyPoint> WeeklySales { get; set; } = [];

    public IReadOnlyList<SmartStockDemoSegmentSummary> SegmentSummaries { get; set; } = [];

    public IReadOnlyList<SmartStockDemoRiskItem> StockRisks { get; set; } = [];

    public IReadOnlyList<SmartStockDemoPurchaseItem> PurchaseRecommendations { get; set; } = [];

    public IReadOnlyList<SmartStockDemoMetric> ModelMetrics { get; set; } = [];
}

public sealed class SmartStockDemoWeeklyPoint
{
    public string WeekStart { get; set; } = string.Empty;

    public string Segment { get; set; } = string.Empty;

    public int ActualSales { get; set; }

    public int ForecastSales { get; set; }
}

public sealed class SmartStockDemoSegmentSummary
{
    public string Segment { get; set; } = string.Empty;

    public int ActualSales { get; set; }

    public int ForecastSales { get; set; }

    public int Variance { get; set; }
}

public sealed class SmartStockDemoRiskItem
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string Segment { get; set; } = string.Empty;

    public int CurrentStock { get; set; }

    public int ForecastDemand { get; set; }

    public int SafetyStock { get; set; }

    public int EstimatedRemainingStock { get; set; }

    public int RiskScore { get; set; }

    public string RiskLevel { get; set; } = string.Empty;

    public string RiskReason { get; set; } = string.Empty;

    public string RecommendedAction { get; set; } = string.Empty;
}

public sealed class SmartStockDemoPurchaseItem
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public int CurrentStock { get; set; }

    public int ForecastDemand { get; set; }

    public int SafetyStock { get; set; }

    public int EstimatedRemainingStock { get; set; }

    public int SuggestedQuantity { get; set; }

    public string Priority { get; set; } = string.Empty;

    public string RecommendationReason { get; set; } = string.Empty;
}

public sealed class SmartStockDemoMetric
{
    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

public class CategoryStockChartItem
{
    public string CategoryName { get; set; } = string.Empty;

    public int QtyPieces { get; set; }
}

public class DailyMovementChartItem
{
    public string Label { get; set; } = string.Empty;

    public int InPieces { get; set; }

    public int OutPieces { get; set; }
}

public class MonthlyMovementChartItem
{
    public string Label { get; set; } = string.Empty;

    public int InPieces { get; set; }

    public int OutPieces { get; set; }
}

public class StockAlertItem
{
    public string CategoryName { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public int QtyPieces { get; set; }

    public int QtyCases { get; set; }
}

public class TopUsedProductItem
{
    public string Sku { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public int UsedQuantity { get; set; }

    public int CurrentStock { get; set; }

    public decimal? ChangePercent { get; set; }

    public bool HasPreviousPeriodUsage { get; set; }

    public IReadOnlyList<int> TrendBuckets { get; set; } = [];
}
