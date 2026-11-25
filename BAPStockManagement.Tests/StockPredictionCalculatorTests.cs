using BAPStockManagement.Services;
using BAPStockManagement.ViewModels.Home;
using Xunit;

namespace BAPStockManagement.Tests;

public class StockPredictionCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 11, 25);

    [Fact]
    public void Calculate_UsesThirtyDaySalesAndPredictsRiskAndDate()
    {
        var result = StockPredictionCalculator.Calculate(
            1,
            "BLB-A032",
            "Test product",
            "ชิ้น",
            10,
            [new StockSalesEntry(Today, 90)],
            Today);

        Assert.Equal(3m, result.AverageDailySales);
        Assert.Equal(10m / 3m, result.DaysUntilStockout);
        Assert.Equal(new DateOnly(2026, 11, 29), result.EstimatedStockoutDate);
        Assert.Equal(StockRiskLevel.Warning, result.RiskLevel);
        Assert.Equal(StockSalesTrend.Increasing, result.SalesTrend);
    }

    [Fact]
    public void Calculate_UsesSixtyDayWindowWhenRecentThirtyDaysHaveNoSales()
    {
        var result = StockPredictionCalculator.Calculate(
            1,
            "SKU-60",
            "Test product",
            "ลัง",
            60,
            [new StockSalesEntry(Today.AddDays(-40), 60)],
            Today);

        Assert.Equal(60, result.AnalysisWindowDays);
        Assert.Equal(1m, result.AverageDailySales);
        Assert.Equal(StockSalesTrend.Decreasing, result.SalesTrend);
    }

    [Fact]
    public void Calculate_UsesNinetyDayWindowWhenRecentSixtyDaysHaveNoSales()
    {
        var result = StockPredictionCalculator.Calculate(
            1,
            "SKU-90",
            "Test product",
            "ชิ้น",
            90,
            [new StockSalesEntry(Today.AddDays(-80), 90)],
            Today);

        Assert.Equal(90, result.AnalysisWindowDays);
        Assert.Equal(1m, result.AverageDailySales);
        Assert.Equal(StockSalesTrend.Stable, result.SalesTrend);
    }

    [Fact]
    public void Calculate_DoesNotForecastWithoutSalesHistory()
    {
        var result = StockPredictionCalculator.Calculate(
            1,
            "SKU-NONE",
            "Test product",
            "ชิ้น",
            12,
            [],
            Today);

        Assert.Equal("ข้อมูลไม่เพียงพอสำหรับการคาดการณ์", result.Recommendation);
        Assert.Null(result.AverageDailySales);
        Assert.Null(result.DaysUntilStockout);
        Assert.Null(result.EstimatedStockoutDate);
        Assert.Equal(StockRiskLevel.InsufficientData, result.RiskLevel);
    }

    [Fact]
    public void Calculate_MarksEmptyStockAsCriticalEvenWithoutSalesHistory()
    {
        var result = StockPredictionCalculator.Calculate(
            1,
            "SKU-EMPTY",
            "Test product",
            "ชิ้น",
            0,
            [],
            Today);

        Assert.Equal(StockRiskLevel.Critical, result.RiskLevel);
        Assert.Equal(0m, result.DaysUntilStockout);
        Assert.Equal(Today, result.EstimatedStockoutDate);
    }

    [Theory]
    [InlineData(3, StockRiskLevel.Critical)]
    [InlineData(7, StockRiskLevel.Warning)]
    [InlineData(8, StockRiskLevel.Normal)]
    public void Calculate_UsesConfiguredRiskBoundaries(int currentStock, StockRiskLevel expectedRisk)
    {
        var result = StockPredictionCalculator.Calculate(
            1,
            "SKU-RISK",
            "Test product",
            "ชิ้น",
            currentStock,
            [new StockSalesEntry(Today, 30)],
            Today);

        Assert.Equal(expectedRisk, result.RiskLevel);
    }
}
