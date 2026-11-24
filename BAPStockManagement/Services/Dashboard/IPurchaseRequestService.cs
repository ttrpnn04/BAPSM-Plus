using BAPStockManagement.Contracts.Dashboard;

namespace BAPStockManagement.Services.Dashboard;

public interface IPurchaseRequestService
{
    Task<PurchaseRequestEnqueueResult> EnqueueAsync(
        string userId,
        string idempotencyKey,
        AutoGeneratePurchaseRequestDto request,
        CancellationToken cancellationToken);

    Task<PurchaseRequestJobDto?> GetJobAsync(
        Guid jobId,
        string userId,
        CancellationToken cancellationToken);
}

public sealed record PurchaseRequestEnqueueResult(
    PurchaseRequestJobDto? Job,
    bool KeyConflict);
