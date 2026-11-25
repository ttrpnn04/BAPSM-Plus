using BAPStockManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace BAPStockManagement.Data;

public static class ExecutionStrategyExtensions
{
    public static Task<TResult> ExecuteWithRetryAsync<TResult>(
        this BAPStockContext context,
        Func<Task<TResult>> operation)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            return await operation();
        });
    }
}
