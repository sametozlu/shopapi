using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopAPI.Application;

namespace ShopAPI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AddressesController : ControllerBase
{
    private readonly IAddressService _addressService;

    public AddressesController(IAddressService addressService) => _addressService = addressService;

    [HttpGet]
    public async Task<IActionResult> GetMy() => Ok(await _addressService.GetMyAddressesAsync(GetUserId()));

    [HttpPost]
    public async Task<IActionResult> Create(AddressRequest request) => Ok(await _addressService.CreateAsync(GetUserId(), request));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, AddressRequest request)
    {
        var updated = await _addressService.UpdateAsync(GetUserId(), id, request);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _addressService.DeleteAsync(GetUserId(), id);
        return deleted ? NoContent() : NotFound();
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User id claim missing.");
        return Guid.Parse(claim);
    }
}
