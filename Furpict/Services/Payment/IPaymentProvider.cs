namespace Furpict.Services.Payment;

public sealed record CreateCheckoutRequest(
    Guid PetModelId,
    string UserId,
    string SuccessUrl,
    string CancelUrl);

public sealed record CheckoutSessionResult(string SessionId, string CheckoutUrl);

public sealed record PaymentVerificationResult(bool IsValid, string? CheckoutSessionId, string? PaymentIntentId);

public interface IPaymentProvider
{
    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(CreateCheckoutRequest request, CancellationToken ct = default);
    Task<PaymentVerificationResult> VerifyWebhookAsync(string payload, string signature, CancellationToken ct = default);
}
