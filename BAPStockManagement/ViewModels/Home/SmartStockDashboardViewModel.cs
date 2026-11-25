namespace BAPStockManagement.ViewModels.Home;

public sealed class SmartStockDashboardViewModel
{
    public DateOnly ForecastWeek { get; init; }
    public DateOnly? ActualsThrough { get; init; }
    public DateOnly? ActualsFrom { get; init; }
    public long? ActualSales { get; init; }
    public double? ForecastSales { get; init; }
    public int ProductCount { get; init; }
    public string? ForecastStatus { get; init; }
    public IReadOnlyList<SmartStockSalesPoint> WeeklySales { get; init; } = [];
    public IReadOnlyList<SmartStockSegmentSummary> Segments { get; init; } = [];
    public IReadOnlyList<SmartStockAnalysis> Analyses { get; init; } = [];
}

public sealed record SmartStockSalesPoint(DateOnly WeekStart, string Segment, long? Actual, double? Forecast);

public sealed record SmartStockSegmentSummary(
    string Segment, string Label, long? LastWeekActual, double? Forecast, string? Model);

public sealed record SmartStockAnalysis(
    string Sku, string Unit, string SegmentLabel, string Model, decimal SalesShare,
    SmartStockDemoRiskItem Risk, SmartStockDemoPurchaseItem Purchase);

public sealed record SmartStockAnalysisListViewModel(
    IReadOnlyList<SmartStockAnalysis> Items, bool Buying, string EmptyMessage);
