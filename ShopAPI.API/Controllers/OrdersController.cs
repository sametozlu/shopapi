using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopAPI.Application;
using ShopAPI.Domain;

namespace ShopAPI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
    {
        var order = await _orderService.CreateOrderAsync(GetUserId(), request);
        return order is null ? BadRequest(new { message = "Cart is empty or stock is insufficient." }) : Ok(order);
    }

    [HttpGet("me")]
    public async Task<IActionResult> MyOrders()
    {
        return Ok(await _orderService.GetMyOrdersAsync(GetUserId()));
    }

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _orderService.GetAllOrdersAsync());
    }

    [HttpPost("{id:guid}/pay")]
    public async Task<IActionResult> Pay(Guid id)
    {
        try
        {
            var result = await _orderService.PayOrderAsync(GetUserId(), id);
            return result is null
                ? BadRequest(new { message = "Order not found or not payable." })
                : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/confirm-payment")]
    public async Task<IActionResult> ConfirmPayment(Guid id, [FromQuery] string sessionId)
    {
        var order = await _orderService.ConfirmPaymentAsync(GetUserId(), id, sessionId);
        return order is null
            ? BadRequest(new { message = "Payment could not be confirmed." })
            : Ok(order);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var order = await _orderService.CancelOrderAsync(GetUserId(), id, false);
        return order is null
            ? BadRequest(new { message = "Order not found or cannot be cancelled." })
            : Ok(order);
    }

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromQuery] OrderStatus status)
    {
        var order = await _orderService.UpdateStatusAsync(id, status);
        return order is null ? NotFound() : Ok(order);
    }

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPost("{id:guid}/admin/cancel")]
    public async Task<IActionResult> AdminCancel(Guid id)
    {
        var order = await _orderService.CancelOrderAsync(Guid.Empty, id, true);
        return order is null
            ? BadRequest(new { message = "Order not found or cannot be cancelled." })
            : Ok(order);
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User id claim missing.");
        return Guid.Parse(claim);
    }
}
