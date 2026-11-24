using System.ComponentModel.DataAnnotations;
using BAPStockManagement.Contracts.Dashboard;
using BAPStockManagement.Services.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BAPStockManagement.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/v1/ai")]
public sealed class AiApiController : ApiControllerBase
{
    [HttpPost("chat")]
    [ValidateAntiForgeryToken]
    public IActionResult Chat(
        [FromBody] AiChatRequestDto request,
        [FromHeader(Name = "Idempotency-Key")]
        [Required]
        [StringLength(128, MinimumLength = 8)]
        [RegularExpression("^[A-Za-z0-9._:-]{8,128}$")]
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Response.Headers["Idempotency-Key"] = idempotencyKey;

        var response = new AiChatResponseDto(
            request.ConversationId ?? Guid.NewGuid().ToString("N"),
            "ได้รับข้อความแล้ว ขณะนี้ AI Chat อยู่ในโหมดตัวอย่าง",
            DateTimeOffset.UtcNow);
        return ApiOk(response);
    }
}
