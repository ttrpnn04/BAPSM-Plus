namespace BAPStockManagement.Contracts.Dashboard;

public sealed record ApiResponse<T>(T Data, ApiMeta Meta);

public sealed record ApiMeta(string RequestId, DateTimeOffset GeneratedAt);

public sealed record MoneyDto(decimal Amount, string Currency);
