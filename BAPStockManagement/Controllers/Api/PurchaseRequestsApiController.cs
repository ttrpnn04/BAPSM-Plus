using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BAPStockManagement.Contracts.Dashboard;
using BAPStockManagement.Services.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BAPStockManagement.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/v1/purchase-requests")]
public sealed class PurchaseRequestsApiController : ApiControllerBase
{
    private readonly IPurchaseRequestService _purchaseRequestService;

    public PurchaseRequestsApiController(IPurchaseRequestService purchaseRequestService)
    {
        _purchaseRequestService = purchaseRequestService;
    }

    [HttpPost("auto-generate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AutoGenerate(
        [FromBody] AutoGeneratePurchaseRequestDto request,
        [FromHeader(Name = "Idempotency-Key")]
        [Required]
        [StringLength(128, MinimumLength = 8)]
        [RegularExpression("^[A-Za-z0-9._:-]{8,128}$")]
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? string.Empty;
        var result = await _purchaseRequestService.EnqueueAsync(
            userId,
            idempotencyKey,
            request,
            cancellationToken);

        if (result.KeyConflict)
        {
            return ApiProblem(
                409,
                "Idempotency key conflict",
                "This Idempotency-Key was already used with a different request body.");
        }

        var job = result.Job
            ?? throw new InvalidOperationException("The purchase request service returned no job.");

        return AcceptedAtRoute(
            "DashboardPurchaseRequestJob",
            new { jobId = job.JobId },
            new ApiResponse<PurchaseRequestJobDto>(
                job,
                new ApiMeta(HttpContext.TraceIdentifier, DateTimeOffset.UtcNow)));
    }

    [HttpGet("jobs/{jobId:guid}", Name = "DashboardPurchaseRequestJob")]
    public async Task<IActionResult> GetJob(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? string.Empty;
        var job = await _purchaseRequestService.GetJobAsync(
            jobId,
            userId,
            cancellationToken);

        return job is null
            ? ApiProblem(404, "Job not found", "No purchase request job exists with the requested identifier.")
            : ApiOk(job);
    }
}
