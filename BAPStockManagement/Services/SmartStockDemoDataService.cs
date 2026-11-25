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

        var openingStockShortfall = Math.Max(0, -input.CurrentStock);
        var uncoveredForecastDemand = Math.Max(0, input.ForecastDemand - Math.Max(0, input.CurrentStock));
        var safetyStockTopUp = Math.Max(0, input.SafetyStock - Math.Max(0, estimatedRemaining));
        var recommendedQuantity = openingStockShortfall + uncoveredForecastDemand + safetyStockTopUp;

        var priority = riskLevel == "High"
            ? "Urgent"
            : riskLevel == "Medium"
                ? "Medium"
                : "Low";

        var explanation = (openingStockShortfall, uncoveredForecastDemand, safetyStockTopUp) switch
        {
            (> 0, 0, 0) => "ชดเชยยอดสต็อกติดลบที่บันทึกไว้ในระบบ",
            (> 0, _, _) => "ชดเชยยอดสต็อกติดลบ และเติมให้พอกับความต้องการของสัปดาห์",
            (_, > 0, > 0) => "เติมให้พอกับยอดขายที่คาดและเหลือสต็อกสำรองตามเป้า",
            (_, > 0, _) => "เติมให้พอกับยอดขายที่คาดในสัปดาห์ที่เลือก",
            (_, _, > 0) => "สต็อกพอกับยอดขายที่คาด แต่หลังขายจะเหลือสำรองต่ำกว่าเป้า",
            _ => "สต็อกครอบคลุมยอดขายที่คาด และเหลือสำรองตามเป้า"
        };

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
            OpeningStockShortfall = openingStockShortfall,
            UncoveredForecastDemand = uncoveredForecastDemand,
            SafetyStockTopUp = safetyStockTopUp,
            Priority = priority,
            RecommendationReason = explanation,
        };

        return (risk, purchase);
    }
}
