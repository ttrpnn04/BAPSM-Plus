using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BAPStockManagement.Contracts.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace BAPStockManagement.Services.Dashboard;

public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult ApiOk<T>(T data)
        => Ok(CreateResponse(data));

    protected IActionResult ApiOkCached<T>(T data)
    {
        var serializedData = JsonSerializer.Serialize(data, ApiJson.SerializerOptions);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(serializedData));
        var entityTag = $"\"{Convert.ToHexString(hash).ToLowerInvariant()}\"";
        Response.Headers.ETag = entityTag;

        var requestedTags = Request.Headers["If-None-Match"]
            .ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (requestedTags.Any(tag =>
                tag == "*"
                || string.Equals(tag, entityTag, StringComparison.Ordinal)
                || tag.StartsWith("W/", StringComparison.Ordinal)
                    && string.Equals(tag[2..], entityTag, StringComparison.Ordinal)))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return ApiOk(data);
    }

    protected ObjectResult ApiProblem(int statusCode, string title, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = Request.Path
        };
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;

        var result = StatusCode(statusCode, problem);
        result.ContentTypes.Add("application/problem+json");
        return result;
    }

    private ApiResponse<T> CreateResponse<T>(T data)
        => new(
            data,
            new ApiMeta(HttpContext.TraceIdentifier, DateTimeOffset.UtcNow));
}
