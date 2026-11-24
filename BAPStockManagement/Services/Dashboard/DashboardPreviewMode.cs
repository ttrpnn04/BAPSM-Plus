using System.Net;

namespace BAPStockManagement.Services.Dashboard;

public static class DashboardPreviewMode
{
    public static bool IsEnabled(IConfiguration configuration, IHostEnvironment environment)
    {
        var dataSource = configuration[$"{DashboardOptions.SectionName}:DataSource"]
            ?? nameof(DashboardDataSource.Mock);

        return environment.IsDevelopment()
            && configuration.GetValue<bool>(
                $"{DashboardOptions.SectionName}:EnableDevelopmentPreview")
            && string.Equals(
                dataSource,
                nameof(DashboardDataSource.Mock),
                StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsLoopback(HttpContext httpContext)
    {
        var address = httpContext.Connection.RemoteIpAddress;
        return address is not null
            && (IPAddress.IsLoopback(address)
                || address.IsIPv4MappedToIPv6
                    && IPAddress.IsLoopback(address.MapToIPv4()));
    }
}
