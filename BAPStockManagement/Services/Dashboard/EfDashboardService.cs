using BAPStockManagement.Contracts.Dashboard;

namespace BAPStockManagement.Services.Dashboard;

public sealed class EfDashboardService : IDashboardService
{
    public Task<DashboardSummaryDto> GetSummaryAsync(
        CancellationToken cancellationToken)
    {
        // TODO: Map Products, ProductVariants, StockBalances, StockTransactions,
        // TransactionTypes, VwCurrentStock, and VwMonthlySummary to the dashboard contract.
        throw new NotImplementedException(
            "The EF dashboard data source has not been implemented.");
    }

    public Task<IReadOnlyList<ProductSearchResultDto>> SearchProductsAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        // TODO: Search Products by SKU/name and join current variant stock.
        throw new NotImplementedException(
            "The EF dashboard product search has not been implemented.");
    }

    public Task<WhatIfSimulationParameters?> GetWhatIfParametersAsync(
        string sku,
        CancellationToken cancellationToken)
    {
        // TODO: Load stock, demand history, cost, lead-time, and safety-stock policy.
        throw new NotImplementedException(
            "The EF what-if inputs have not been implemented.");
    }
}
