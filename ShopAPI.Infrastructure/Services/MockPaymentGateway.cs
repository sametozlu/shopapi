using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using ShopAPI.Application;
using ShopAPI.Domain;

namespace ShopAPI.Infrastructure;

public class MockPaymentGateway : IPaymentGateway
{
    public Task<PaymentResult> ChargeAsync(Guid userId, decimal amount, string currency = "TRY")
    {
        if (amount <= 0)
            return Task.FromResult(new PaymentResult(false, null, "Invalid payment amount.", "mock"));

        var transactionId = $"MOCK-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32];
        return Task.FromResult(new PaymentResult(true, transactionId, null, "mock"));
    }

    public Task<CheckoutSessionResult> CreateCheckoutSessionAsync(CheckoutSessionRequest request) =>
        Task.FromResult(new CheckoutSessionResult(false, null, null, "Mock provider does not use checkout sessions.", "mock"));
}
