using System.ComponentModel.DataAnnotations;

namespace BAPStockManagement.Contracts.Dashboard;

public sealed record MeDto(
    string UserId,
    string? UserName,
    string? Email,
    IReadOnlyList<string> Roles);

public sealed record UnreadNotificationCountDto(int Count);

public sealed record ProductSearchResultDto(
    string Sku,
    string ProductName,
    int OnHand,
    string Unit);

public sealed record DashboardForecastDto(
    string Horizon,
    IReadOnlyList<ForecastPointDto> Points);

public sealed record WhatIfSimulationRequest : IValidatableObject
{
    [Required]
    [StringLength(50)]
    [RegularExpression("^[A-Za-z0-9._-]+$")]
    public string Sku { get; init; } = string.Empty;

    [Range(100, 1000)]
    public int OrderQty { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if ((OrderQty - 100) % 100 != 0)
        {
            yield return new ValidationResult(
                "OrderQty must use increments of 100.",
                [nameof(OrderQty)]);
        }
    }
}

public enum PurchaseRequestStrategy
{
    AiRecommended
}

public sealed record AutoGeneratePurchaseRequestDto : IValidatableObject
{
    [Required]
    [EnumDataType(typeof(PurchaseRequestStrategy))]
    public PurchaseRequestStrategy? Strategy { get; init; }

    [MaxLength(100)]
    public IReadOnlyList<string>? Skus { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Skus is null)
        {
            yield break;
        }

        for (var index = 0; index < Skus.Count; index++)
        {
            var sku = Skus[index];
            if (string.IsNullOrWhiteSpace(sku)
                || sku.Length > 50
                || sku.Any(character =>
                    !char.IsAsciiLetterOrDigit(character)
                    && character is not '.' and not '_' and not '-'))
            {
                yield return new ValidationResult(
                    $"Skus[{index}] must contain between 1 and 50 characters.",
                    [nameof(Skus)]);
            }
        }
    }
}

public enum PurchaseRequestJobStatus
{
    Queued,
    Completed,
    Failed
}

public sealed record PurchaseRequestJobDto(
    Guid JobId,
    PurchaseRequestJobStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? Message);

public sealed record AiChatRequestDto
{
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    [RegularExpression(@"\S")]
    public string Message { get; init; } = string.Empty;

    [StringLength(100)]
    public string? ConversationId { get; init; }
}

public sealed record AiChatResponseDto(
    string ConversationId,
    string Reply,
    DateTimeOffset RepliedAt);
