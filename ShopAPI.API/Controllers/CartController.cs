using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopAPI.Application;

namespace ShopAPI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CartController : ControllerBase
{
    private readonly ICartService _cartService;

    public CartController(ICartService cartService)
    {
        _cartService = cartService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyCart()
    {
        return Ok(await _cartService.GetMyCartAsync(GetUserId()));
    }

    [HttpPost("items")]
    public async Task<IActionResult> AddItem(AddCartItemRequest request)
    {
        var success = await _cartService.AddItemAsync(GetUserId(), request);
        return success ? Ok() : BadRequest(new { message = "Product is unavailable or stock is not enough." });
    }

    [HttpPut("items/{productId:guid}")]
    public async Task<IActionResult> UpdateItem(Guid productId, UpdateCartItemRequest request)
    {
        var success = await _cartService.UpdateItemAsync(GetUserId(), productId, request);
        return success ? Ok() : BadRequest(new { message = "Cart item not found or invalid quantity." });
    }

    [HttpDelete("items/{productId:guid}")]
    public async Task<IActionResult> RemoveItem(Guid productId)
    {
        var success = await _cartService.RemoveItemAsync(GetUserId(), productId);
        return success ? NoContent() : NotFound();
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User id claim missing.");
        return Guid.Parse(claim);
    }
}
