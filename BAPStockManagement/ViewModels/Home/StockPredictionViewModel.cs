namespace BAPStockManagement.ViewModels.Home;

public enum StockRiskLevel
{
    Critical,
    Warning,
    Normal,
    InsufficientData
}

public enum StockSalesTrend
{
    Increasing,
    Decreasing,
    Stable,
    InsufficientData
}

public enum StockInboundTrend
{
    Increasing,
    Decreasing,
    Stable,
    InsufficientData
}

public enum SmartRestockPriority
{
    Urgent,
    Prepare,
    Monitor
}

public sealed class StockPredictionViewModel
{
    public int ProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public int CurrentStock { get; set; }

    public decimal? AverageDailySales { get; set; }

    public decimal? DaysUntilStockout { get; set; }

    public DateOnly? EstimatedStockoutDate { get; set; }

    public StockSalesTrend SalesTrend { get; set; }

    public StockRiskLevel RiskLevel { get; set; }

    public int? AnalysisWindowDays { get; set; }

    public bool IsLowStock { get; set; }

    public string Recommendation { get; set; } = string.Empty;

    public IReadOnlyList<StockPredictionChartPoint> SalesHistory { get; set; } = [];
}

public sealed class StockPredictionChartPoint
{
    public string Label { get; set; } = string.Empty;

    public int Quantity { get; set; }
}

public sealed class StockInboundTrendViewModel
{
    public int ProductId { get; set; }

    public int Recent30DayQuantity { get; set; }

    public int Previous30DayQuantity { get; set; }

    public StockInboundTrend Trend { get; set; }

    public IReadOnlyList<StockPredictionChartPoint> History { get; set; } = [];
}

public sealed class SmartRestockItemViewModel
{
    public StockPredictionViewModel Prediction { get; set; } = new();

    public StockInboundTrendViewModel InboundTrend { get; set; } = new();

    public SmartRestockPriority Priority { get; set; }

    public string PriorityLabel { get; set; } = string.Empty;

    public string DecisionReason { get; set; } = string.Empty;

    public string InboundTrendLabel { get; set; } = string.Empty;
}
