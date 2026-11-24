namespace BAPStockManagement.Services.Dashboard;

public sealed record WhatIfSimulationParameters(
    string Sku,
    string ProductName,
    decimal AverageDailyDemand,
    int LeadTimeDays,
    int SafetyStock,
    decimal UnitCost,
    decimal AnnualHoldingRate,
    decimal OrderFee,
    decimal RiskUncertaintyFactor,
    int MinimumOrderQuantity,
    int MaximumOrderQuantity,
    int OrderQuantityStep,
    IReadOnlyList<int> QuantityTicks);
