namespace Furpict.Data.Entities;

public sealed class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public Guid PetModelId { get; set; }
    public int AmountCents { get; set; }
    public string Currency { get; set; } = "usd";
    public string StripeCheckoutSessionId { get; set; } = string.Empty;
    public string? StripePaymentIntentId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public PetModel PetModel { get; set; } = null!;
}
