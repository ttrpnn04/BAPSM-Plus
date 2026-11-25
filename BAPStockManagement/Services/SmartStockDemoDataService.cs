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
            ? $"สต็อกต่ำกว่ายอดที่คาด {Math.Abs(estimatedRemaining):N0} หน่วย"
            : estimatedRemaining < input.SafetyStock
                ? "หลังขายตามคาด สต็อกจะต่ำกว่ายอดสำรอง"
                : "สต็อกครอบคลุมยอดที่คาดและยอดสำรอง";

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
                ? $"เตรียมสั่งเพิ่ม {recommendedQuantity:N0} หน่วย"
                : "ยังไม่ต้องสั่งเพิ่ม",
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
            RecommendationReason = explanation,
        };

        return (risk, purchase);
    }
}
