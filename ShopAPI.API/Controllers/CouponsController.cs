using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopAPI.Application;
using ShopAPI.Domain;

namespace ShopAPI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CouponsController : ControllerBase
{
    private readonly ICouponService _couponService;

    public CouponsController(ICouponService couponService) => _couponService = couponService;

    [HttpGet("{code}")]
    public async Task<IActionResult> GetByCode(string code)
    {
        var coupon = await _couponService.GetByCodeAsync(code);
        return coupon is null ? NotFound() : Ok(coupon);
    }

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _couponService.GetAllAsync());

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPost]
    public async Task<IActionResult> Create(CouponRequest request) => Ok(await _couponService.CreateAsync(request));

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, CouponRequest request)
    {
        var coupon = await _couponService.UpdateAsync(id, request);
        return coupon is null ? NotFound() : Ok(coupon);
    }
}
