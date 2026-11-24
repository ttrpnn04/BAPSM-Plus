using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BAPStockManagement.Contracts.Dashboard;

namespace BAPStockManagement.Services.Dashboard;

public sealed class MockPurchaseRequestService : IPurchaseRequestService
{
    private readonly ConcurrentDictionary<string, IdempotencyEntry> _requests =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<Guid, OwnedJob> _jobs = new();

    public Task<PurchaseRequestEnqueueResult> EnqueueAsync(
        string userId,
        string idempotencyKey,
        AutoGeneratePurchaseRequestDto request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var payload = JsonSerializer.Serialize(request, ApiJson.SerializerOptions);
        var fingerprint = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        var storageKey = $"{userId}\0{idempotencyKey}";
        var entry = _requests.GetOrAdd(
            storageKey,
            _ => CreateEntry(userId, request, fingerprint));

        if (!string.Equals(entry.Fingerprint, fingerprint, StringComparison.Ordinal))
        {
            return Task.FromResult(new PurchaseRequestEnqueueResult(null, KeyConflict: true));
        }

        _jobs.TryAdd(entry.Job.JobId, new OwnedJob(userId, entry.Job));
        return Task.FromResult(new PurchaseRequestEnqueueResult(entry.Job, KeyConflict: false));
    }

    public Task<PurchaseRequestJobDto?> GetJobAsync(
        Guid jobId,
        string userId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            _jobs.TryGetValue(jobId, out var ownedJob)
                && string.Equals(ownedJob.UserId, userId, StringComparison.Ordinal)
                    ? ownedJob.Job
                    : null);
    }

    private static IdempotencyEntry CreateEntry(
        string userId,
        AutoGeneratePurchaseRequestDto request,
        string fingerprint)
    {
        var now = DateTimeOffset.UtcNow;
        var job = new PurchaseRequestJobDto(
            Guid.NewGuid(),
            PurchaseRequestJobStatus.Completed,
            now,
            now,
            "Mock job completed; no purchase request was written to a database.");

        return new IdempotencyEntry(userId, fingerprint, job);
    }

    private sealed record IdempotencyEntry(
        string UserId,
        string Fingerprint,
        PurchaseRequestJobDto Job);

    private sealed record OwnedJob(string UserId, PurchaseRequestJobDto Job);
}
