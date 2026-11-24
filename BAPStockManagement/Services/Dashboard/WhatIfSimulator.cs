using BAPStockManagement.Contracts.Dashboard;

namespace BAPStockManagement.Services.Dashboard;

public static class WhatIfSimulator
{
    /// <summary>
    /// Calculates deterministic demo outputs from explicit mock assumptions.
    /// Assumptions: average demand is 25/6 units per day; lead time is 6 days;
    /// safety stock is 25 units; unit cost is ฿150; annual holding rate is 7/30;
    /// order fee is ฿0; risk uncertainty factor is 0.24. At quantity 300, lead-time
    /// demand rounds up to 25, risk is (25 + 25) * 0.24 / 300 * 100 = 4%,
    /// order cost is 300 * 150 = ฿45,000, holding cost is 300 * 150 * 7/30
    /// = ฿10,500, and coverage is rounded to the nearest day from 300 / (25/6)
    /// = 72 days. These values
    /// are demo assumptions, not estimates from the existing database.
    /// </summary>
    public static WhatIfSimulationDto Calculate(
        WhatIfSimulationParameters parameters,
        int orderQuantity)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (parameters.AverageDailyDemand <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(parameters),
                "Average daily demand must be greater than zero.");
        }

        if (parameters.LeadTimeDays < 0
            || parameters.SafetyStock < 0
            || parameters.UnitCost < 0
            || parameters.AnnualHoldingRate < 0
            || parameters.OrderFee < 0
            || parameters.RiskUncertaintyFactor < 0
            || parameters.OrderQuantityStep <= 0
            || parameters.MinimumOrderQuantity <= 0
            || parameters.MaximumOrderQuantity < parameters.MinimumOrderQuantity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(parameters),
                "Simulation assumptions must be non-negative and the quantity range must be valid.");
        }

        if (orderQuantity < parameters.MinimumOrderQuantity
            || orderQuantity > parameters.MaximumOrderQuantity
            || (orderQuantity - parameters.MinimumOrderQuantity)
                % parameters.OrderQuantityStep != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(orderQuantity),
                "Order quantity must be within the configured range and step.");
        }

        var leadTimeDemand = decimal.Ceiling(
            parameters.AverageDailyDemand * parameters.LeadTimeDays);
        var stockoutRisk = Math.Clamp(
            (leadTimeDemand + parameters.SafetyStock)
                * parameters.RiskUncertaintyFactor
                / orderQuantity
                * 100m,
            0m,
            100m);
        var orderCost = decimal.Round(
            orderQuantity * parameters.UnitCost + parameters.OrderFee,
            2,
            MidpointRounding.AwayFromZero);
        var holdingCost = decimal.Round(
            orderQuantity * parameters.UnitCost * parameters.AnnualHoldingRate,
            2,
            MidpointRounding.AwayFromZero);
        var coverageDays = (int)decimal.Round(
            orderQuantity / parameters.AverageDailyDemand,
            0,
            MidpointRounding.AwayFromZero);

        return new WhatIfSimulationDto(
            parameters.Sku,
            parameters.ProductName,
            parameters.MinimumOrderQuantity,
            parameters.MaximumOrderQuantity,
            parameters.OrderQuantityStep,
            parameters.QuantityTicks,
            orderQuantity,
            decimal.Round(stockoutRisk, 1, MidpointRounding.AwayFromZero),
            new MoneyDto(orderCost, "THB"),
            new MoneyDto(holdingCost, "THB"),
            coverageDays);
    }
}
