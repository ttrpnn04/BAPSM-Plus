using System.Security.Claims;
using BAPStockManagement.Contracts.Dashboard;
using BAPStockManagement.Services.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BAPStockManagement.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/v1/me")]
public sealed class MeApiController : ApiControllerBase
{
    [HttpGet]
    public IActionResult Get(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var roles = User.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var response = new MeDto(
            userId,
            User.Identity?.Name,
            User.FindFirstValue(ClaimTypes.Email),
            roles);

        return ApiOk(response);
    }
}
