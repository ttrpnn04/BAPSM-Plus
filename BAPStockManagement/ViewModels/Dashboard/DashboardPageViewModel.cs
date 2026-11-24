using BAPStockManagement.Contracts.Dashboard;

namespace BAPStockManagement.ViewModels.Dashboard;

public sealed record DashboardPageViewModel(
    DashboardSummaryDto Summary,
    string DisplayName,
    string Department,
    DateOnly ReportDate,
    string ReportDateLabel);
