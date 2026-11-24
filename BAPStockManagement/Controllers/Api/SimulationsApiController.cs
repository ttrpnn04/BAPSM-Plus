using System.ComponentModel.DataAnnotations;
using BAPStockManagement.Contracts.Dashboard;
using BAPStockManagement.Services.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BAPStockManagement.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/v1/simulations")]
public sealed class SimulationsApiController : ApiControllerBase
{
    private readonly IDashboardService _dashboardService;

    public SimulationsApiController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpPost("what-if")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WhatIf(
        [FromBody] WhatIfSimulationRequest request,
        [FromHeader(Name = "Idempotency-Key")]
        [Required]
        [StringLength(128, MinimumLength = 8)]
        [RegularExpression("^[A-Za-z0-9._:-]{8,128}$")]
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Response.Headers["Idempotency-Key"] = idempotencyKey;

        var parameters = await _dashboardService.GetWhatIfParametersAsync(
            request.Sku,
            cancellationToken);
        if (parameters is null)
        {
            return ApiProblem(404, "Product not found", "The requested SKU is not available for simulation.");
        }

        return ApiOk(WhatIfSimulator.Calculate(parameters, request.OrderQty));
    }
}
