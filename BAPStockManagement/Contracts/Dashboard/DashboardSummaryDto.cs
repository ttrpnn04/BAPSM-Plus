namespace BAPStockManagement.Contracts.Dashboard;

public sealed record DashboardSummaryDto(
    DashboardKpisDto Kpis,
    int UnreadNotificationCount,
    IReadOnlyList<ForecastPointDto> Forecast,
    IReadOnlyList<StockStatusDto> StockStatus,
    IReadOnlyList<UrgentItemDto> UrgentItems,
    AiInsightsDto? AiInsights,
    IReadOnlyList<TopProductDto> TopProducts,
    WhatIfSimulationDto WhatIf);

public sealed record DashboardKpisDto(
    int TotalProducts,
    MoneyDto InventoryValue,
    int LowStockProducts,
    int RecommendedReorders,
    int ProductCountChangePercent,
    int InventoryValueChangePercent,
    int LowStockChange);

public sealed record ForecastPointDto(
    string Month,
    decimal? Actual,
    decimal? Forecast,
    decimal? ConfidenceLow,
    decimal? ConfidenceHigh);

public sealed record StockStatusDto(string Status, int Percentage, int Count);

public sealed record UrgentItemDto(
    string Sku,
    string ProductName,
    int DaysUntilStockout,
    int OnHand,
    int RecommendedOrderQuantity);

public sealed record AiInsightsDto(
    string Summary,
    IReadOnlyList<string> Highlights,
    string MascotQuote);

public sealed record TopProductDto(
    string ProductName,
    int Usage30Days,
    int ChangePercent,
    IReadOnlyList<int> Trend);

public sealed record WhatIfSimulationDto(
    string Sku,
    string ProductName,
    int MinimumOrderQuantity,
    int MaximumOrderQuantity,
    int OrderQuantityStep,
    IReadOnlyList<int> QuantityTicks,
    int OrderQuantity,
    decimal StockoutRiskPercent,
    MoneyDto OrderCost,
    MoneyDto HoldingCost,
    int CoverageDays);
