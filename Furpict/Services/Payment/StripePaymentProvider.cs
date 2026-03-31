using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace Furpict.Services.Payment;

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";
    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public int PriceAmountCents { get; set; } = 2999;
    public string Currency { get; set; } = "usd";
}

internal sealed class StripePaymentProvider(IOptions<StripeOptions> options) : IPaymentProvider
{
    private readonly StripeOptions _options = options.Value;

    public async Task<CheckoutSessionResult> CreateCheckoutSessionAsync(CreateCheckoutRequest request, CancellationToken ct = default)
    {
        StripeConfiguration.ApiKey = _options.SecretKey;

        var sessionService = new SessionService();
        var session = await sessionService.CreateAsync(new SessionCreateOptions
        {
            PaymentMethodTypes = ["card"],
            LineItems =
            [
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = _options.Currency,
                        UnitAmount = _options.PriceAmountCents,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = "Furpict Pet AI Model",
                            Description = "One-time payment to create an AI image model of your pet."
                        }
                    },
                    Quantity = 1
                }
            ],
            Mode = "payment",
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            Metadata = new Dictionary<string, string>
            {
                ["pet_model_id"] = request.PetModelId.ToString(),
                ["user_id"] = request.UserId
            }
        }, cancellationToken: ct);

        return new CheckoutSessionResult(session.Id, session.Url);
    }

    public Task<PaymentVerificationResult> VerifyWebhookAsync(string payload, string signature, CancellationToken ct = default)
    {
        try
        {
            var stripeEvent = EventUtility.ConstructEvent(payload, signature, _options.WebhookSecret);
            if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
            {
                var session = (Session)stripeEvent.Data.Object;
                return Task.FromResult(new PaymentVerificationResult(true, session.Id, session.PaymentIntentId));
            }

            return Task.FromResult(new PaymentVerificationResult(true, null, null));
        }
        catch (StripeException)
        {
            return Task.FromResult(new PaymentVerificationResult(false, null, null));
        }
    }
}
