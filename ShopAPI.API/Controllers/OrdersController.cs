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
    public async Task<IActionResult> Create()
    {
        var order = await _orderService.CreateOrderAsync(GetUserId());
        return order is null ? BadRequest(new { message = "Cart is empty or stock is insufficient." }) : Ok(order);
    }

    [HttpGet("me")]
    public async Task<IActionResult> MyOrders()
    {
        return Ok(await _orderService.GetMyOrdersAsync(GetUserId()));
    }

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromQuery] OrderStatus status)
    {
        var order = await _orderService.UpdateStatusAsync(id, status);
        return order is null ? NotFound() : Ok(order);
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User id claim missing.");
        return Guid.Parse(claim);
    }
}
