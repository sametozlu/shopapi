using Microsoft.AspNetCore.Mvc;
using ShopAPI.Application;

namespace ShopAPI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IConfiguration _configuration;

    public PaymentsController(IOrderService orderService, IConfiguration configuration)
    {
        _orderService = orderService;
        _configuration = configuration;
    }

    [HttpGet("settings")]
    public IActionResult Settings() =>
        Ok(new { provider = _configuration["Payments:Provider"]?.ToLowerInvariant() ?? "mock" });

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] PaymentWebhookRequest request, [FromHeader(Name = "X-Webhook-Secret")] string? secret)
    {
        var expected = _configuration["Payments:WebhookSecret"] ?? "shopapi-webhook-secret";
        if (!string.Equals(secret, expected, StringComparison.Ordinal))
            return Unauthorized(new { message = "Invalid webhook secret." });

        var order = await _orderService.HandlePaymentWebhookAsync(request);
        return order is null ? NotFound(new { message = "Payment transaction not found." }) : Ok(order);
    }

    [HttpPost("stripe-webhook")]
    public async Task<IActionResult> StripeWebhook()
    {
        var webhookSecret = _configuration["Stripe:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(webhookSecret))
            return BadRequest(new { message = "Stripe webhook secret is not configured." });

        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"].ToString();
        if (string.IsNullOrWhiteSpace(signature))
            return BadRequest(new { message = "Missing Stripe-Signature header." });

        Stripe.Event stripeEvent;
        try
        {
            stripeEvent = Stripe.EventUtility.ConstructEvent(json, signature, webhookSecret);
        }
        catch (Stripe.StripeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        if (stripeEvent.Type == "checkout.session.completed")
        {
            var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
            if (session is null || string.IsNullOrWhiteSpace(session.Id))
                return BadRequest(new { message = "Invalid checkout session payload." });

            var order = await _orderService.ConfirmStripeWebhookAsync(session.Id);
            return order is null ? NotFound(new { message = "Order not found or not confirmable." }) : Ok(order);
        }

        return Ok(new { received = true, type = stripeEvent.Type });
    }
}
