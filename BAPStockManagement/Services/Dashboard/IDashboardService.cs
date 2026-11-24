using BAPStockManagement.Contracts.Dashboard;

namespace BAPStockManagement.Services.Dashboard;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductSearchResultDto>> SearchProductsAsync(
        string query,
        int limit,
        CancellationToken cancellationToken);

    Task<WhatIfSimulationParameters?> GetWhatIfParametersAsync(
        string sku,
        CancellationToken cancellationToken);
}
