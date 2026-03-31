namespace Furpict.Data.Entities;

public sealed class PetModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PetId { get; set; }
    public ModelStatus Status { get; set; } = ModelStatus.Pending;
    public string? ExternalModelId { get; set; }
    public string? TrainingZipBlobUrl { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public string? StripeCheckoutSessionId { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? TrainingStartedAt { get; set; }
    public DateTimeOffset? TrainingCompletedAt { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Pet Pet { get; set; } = null!;
    public ICollection<GeneratedImage> GeneratedImages { get; set; } = [];
    public Order? Order { get; set; }
}
