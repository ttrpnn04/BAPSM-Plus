using BAPStockManagement.Contracts.Dashboard;
using Microsoft.Extensions.Options;

namespace BAPStockManagement.Services.Dashboard;

public sealed class MockDashboardService : IDashboardService
{
    private static readonly WhatIfSimulationParameters KeyboardSimulation = new(
        Sku: "KB-001",
        ProductName: "Keyboard Logitech K380",
        AverageDailyDemand: 25m / 6m,
        LeadTimeDays: 6,
        SafetyStock: 25,
        UnitCost: 150m,
        AnnualHoldingRate: 7m / 30m,
        OrderFee: 0m,
        RiskUncertaintyFactor: 0.24m,
        MinimumOrderQuantity: 100,
        MaximumOrderQuantity: 1000,
        OrderQuantityStep: 100,
        QuantityTicks: [100, 300, 500, 1000]);

    private static readonly IReadOnlyList<ProductSearchResultDto> Products =
    [
        new("KB-001", "Keyboard Logitech K380", 85, "ชิ้น"),
        new("MS-001", "Mouse Logitech M331", 120, "ชิ้น"),
        new("SSD-001", "SSD 1TB Samsung 990", 24, "ชิ้น"),
        new("CB-003", "USB-C Cable 1M", 310, "ชิ้น"),
        new("MON-024", "Monitor 24″", 18, "ชิ้น")
    ];

    private readonly DashboardOptions _options;

    public MockDashboardService(IOptions<DashboardOptions> options)
    {
        _options = options.Value;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(
        CancellationToken cancellationToken)
    {
        await SimulateLatencyAsync(cancellationToken);

        return new DashboardSummaryDto(
            new DashboardKpisDto(
                TotalProducts: 1248,
                InventoryValue: new MoneyDto(2480500m, "THB"),
                LowStockProducts: 23,
                RecommendedReorders: 15,
                ProductCountChangePercent: 5,
                InventoryValueChangePercent: 12,
                LowStockChange: 4),
            UnreadNotificationCount: 3,
            // ASSUMPTION: estimated series because the reference chart image was not attached.
            Forecast:
            [
                new ForecastPointDto("ม.ค.", 1120m, null, null, null),
                new ForecastPointDto("ก.พ.", 1180m, null, null, null),
                new ForecastPointDto("มี.ค.", 1090m, null, null, null),
                new ForecastPointDto("เม.ย.", 1280m, null, null, null),
                new ForecastPointDto("พ.ค.", 1210m, null, null, null),
                new ForecastPointDto("มิ.ย.", 1390m, null, null, null),
                new ForecastPointDto("ก.ค.", 1460m, null, null, null),
                new ForecastPointDto("ส.ค.", null, 1540m, 1380m, 1700m),
                new ForecastPointDto("ก.ย.", null, 1630m, 1460m, 1800m),
                new ForecastPointDto("ต.ค.", null, 1580m, 1400m, 1760m),
                new ForecastPointDto("พ.ย.", null, 1760m, 1570m, 1950m),
                new ForecastPointDto("ธ.ค.", null, 1880m, 1670m, 2090m)
            ],
            StockStatus:
            [
                new StockStatusDto("NORMAL", 78, 972),
                new StockStatusDto("LOW_STOCK", 12, 152),
                new StockStatusDto("OUT_OF_STOCK", 5, 62),
                new StockStatusDto("OVERSTOCK", 5, 62)
            ],
            UrgentItems:
            [
                new UrgentItemDto("KB-001", "Keyboard Logitech K380", 6, 85, 300),
                new UrgentItemDto("MS-001", "Mouse Logitech M331", 8, 120, 200),
                new UrgentItemDto("SSD-001", "SSD 1TB Samsung 990", 10, 24, 100),
                new UrgentItemDto("CB-003", "USB-C Cable 1M", 12, 310, 500)
            ],
            AiInsights: new AiInsightsDto(
                "สรุปภาพรวมคลังสินค้าของคุณวันนี้",
                [
                    "มีสินค้าในระบบทั้งหมด 1,248 รายการ เพิ่มขึ้น 5% จากเดือนก่อน",
                    "มูลค่าสินค้าในคลัง ฿2,480,500 เพิ่มขึ้น 12% จากเดือนก่อน",
                    "มีสินค้าใกล้หมด 23 รายการ ควรตรวจสอบและวางแผนสั่งซื้อ",
                    "มีรายการแนะนำให้สั่งซื้อ 15 รายการ"
                ],
                "วันนี้มีสินค้า 15 รายการที่ควรสั่งซื้อ เพื่อให้คลังพร้อมใช้งานนะครับ 😊"),
            TopProducts:
            [
                // ASSUMPTION: sparkline samples estimated from the percentage changes in the brief.
                new TopProductDto("USB-C Cable 1M", 1240, 18, [12, 14, 13, 17, 16, 20, 23]),
                new TopProductDto("Mouse Logitech M331", 980, 12, [12, 13, 12, 15, 16, 17, 19]),
                new TopProductDto("Keyboard K380", 820, 5, [12, 11, 13, 12, 14, 14, 15]),
                new TopProductDto("SSD 1TB", 650, 28, [9, 10, 12, 11, 14, 17, 22]),
                new TopProductDto("Monitor 24″", 520, 7, [11, 12, 11, 13, 12, 14, 15])
            ],
            WhatIf: WhatIfSimulator.Calculate(KeyboardSimulation, 300));
    }

    public async Task<IReadOnlyList<ProductSearchResultDto>> SearchProductsAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        await SimulateLatencyAsync(cancellationToken);

        return Products
            .Where(product =>
                product.Sku.Contains(query, StringComparison.OrdinalIgnoreCase)
                || product.ProductName.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .ToArray();
    }

    public async Task<WhatIfSimulationParameters?> GetWhatIfParametersAsync(
        string sku,
        CancellationToken cancellationToken)
    {
        await SimulateLatencyAsync(cancellationToken);
        return string.Equals(sku, KeyboardSimulation.Sku, StringComparison.OrdinalIgnoreCase)
            ? KeyboardSimulation
            : null;
    }

    private async Task SimulateLatencyAsync(CancellationToken cancellationToken)
    {
        if (_options.SimulatedLatencyMs > 0)
        {
            await Task.Delay(_options.SimulatedLatencyMs, cancellationToken);
        }
    }
}
