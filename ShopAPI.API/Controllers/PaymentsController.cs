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

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] PaymentWebhookRequest request, [FromHeader(Name = "X-Webhook-Secret")] string? secret)
    {
        var expected = _configuration["Payments:WebhookSecret"] ?? "shopapi-webhook-secret";
        if (!string.Equals(secret, expected, StringComparison.Ordinal))
            return Unauthorized(new { message = "Invalid webhook secret." });

        var order = await _orderService.HandlePaymentWebhookAsync(request);
        return order is null ? NotFound(new { message = "Payment transaction not found." }) : Ok(order);
    }
}
