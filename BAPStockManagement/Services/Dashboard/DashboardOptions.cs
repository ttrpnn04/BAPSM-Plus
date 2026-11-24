namespace BAPStockManagement.Services.Dashboard;

public sealed class DashboardOptions
{
    public const string SectionName = "Dashboard";

    public DashboardDataSource DataSource { get; set; } = DashboardDataSource.Mock;

    public int SimulatedLatencyMs { get; set; }
}

public enum DashboardDataSource
{
    Mock,
    Ef
}
