namespace Furpict.Client.Models;

public sealed record PetResponse(Guid Id, string Name, string Species, string? Breed, DateTimeOffset CreatedAt);

public sealed record PetDetailResponse(
    Guid Id, string Name, string Species, string? Breed,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PetModelSummary> Models);

public sealed record PetModelSummary(Guid Id, string Status, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt);

public sealed record PetModelDetailResponse(
    Guid Id, Guid PetId, string PetName, string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt, DateTimeOffset? TrainingStartedAt, DateTimeOffset? CompletedAt,
    string? FailureReason);

public sealed record ModelStatusResponse(
    Guid Id, string Status,
    DateTimeOffset? TrainingStartedAt, DateTimeOffset? CompletedAt,
    string? FailureReason);

public sealed record CreateModelResponse(Guid ModelId, string CheckoutUrl);

public sealed record GeneratedImageResponse(Guid Id, string Prompt, string ImageUrl, DateTimeOffset CreatedAt);

public sealed record GalleryImageResponse(Guid Id, string Prompt, string ImageUrl, string? ThumbnailUrl, DateTimeOffset CreatedAt);
