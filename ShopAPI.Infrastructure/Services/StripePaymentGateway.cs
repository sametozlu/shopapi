using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using ShopAPI.Application;
using ShopAPI.Domain;

namespace ShopAPI.Infrastructure;

public class StripePaymentGateway : IPaymentGateway
{
    private readonly string _secretKey;

    public StripePaymentGateway(IConfiguration configuration)
    {
        _secretKey = configuration["Stripe:SecretKey"] ?? string.Empty;
    }

    public Task<PaymentResult> ChargeAsync(Guid userId, decimal amount, string currency = "TRY") =>
        Task.FromResult(new PaymentResult(false, null, "Use Stripe Checkout session flow.", "stripe"));

    public async Task<CheckoutSessionResult> CreateCheckoutSessionAsync(CheckoutSessionRequest request)
    {
        if (request.Amount <= 0)
            return new CheckoutSessionResult(false, null, null, "Invalid payment amount.", "stripe");
        if (string.IsNullOrWhiteSpace(_secretKey))
            return new CheckoutSessionResult(false, null, null, "Stripe secret key is not configured.", "stripe");

        Stripe.StripeConfiguration.ApiKey = _secretKey;
        try
        {
            var lineItems = request.LineItems
                .Where(x => x.UnitAmountMinor != 0)
                .Select(x => new Stripe.Checkout.SessionLineItemOptions
                {
                    PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                    {
                        Currency = request.Currency.ToLowerInvariant(),
                        UnitAmount = Math.Abs(x.UnitAmountMinor),
                        ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions
                        {
                            Name = x.Name
                        }
                    },
                    Quantity = x.Quantity
                })
                .ToList();

            if (lineItems.Count == 0)
            {
                lineItems.Add(new Stripe.Checkout.SessionLineItemOptions
                {
                    PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                    {
                        Currency = request.Currency.ToLowerInvariant(),
                        UnitAmount = (long)Math.Round(request.Amount * 100m, MidpointRounding.AwayFromZero),
                        ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Order {request.OrderId}"
                        }
                    },
                    Quantity = 1
                });
            }

            var session = await new Stripe.Checkout.SessionService().CreateAsync(new Stripe.Checkout.SessionCreateOptions
            {
                Mode = "payment",
                SuccessUrl = request.SuccessUrl,
                CancelUrl = request.CancelUrl,
                LineItems = lineItems,
                Metadata = new Dictionary<string, string>
                {
                    ["orderId"] = request.OrderId.ToString(),
                    ["userId"] = request.UserId.ToString()
                }
            });

            if (string.IsNullOrWhiteSpace(session.Url))
                return new CheckoutSessionResult(false, session.Id, null, "Stripe did not return a checkout URL.", "stripe");

            return new CheckoutSessionResult(true, session.Id, session.Url, null, "stripe");
        }
        catch (Stripe.StripeException ex)
        {
            var message = ex.StripeError?.Message ?? ex.Message;
            return new CheckoutSessionResult(false, null, null, message, "stripe");
        }
    }
}
