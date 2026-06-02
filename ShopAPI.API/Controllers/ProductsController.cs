using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopAPI.Application;
using ShopAPI.Domain;

namespace ShopAPI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductDto>>> Get(
        [FromQuery] string? search,
        [FromQuery] Guid? categoryId,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] bool? isActive,
        [FromQuery] string? sortBy = "name",
        [FromQuery] bool desc = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var products = await _productService.GetAllAsync(
            new ProductQuery(search, categoryId, minPrice, maxPrice, isActive, sortBy, desc, page, pageSize));
        return Ok(products);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> GetById(Guid id)
    {
        var product = await _productService.GetByIdAsync(id);
        return product is null ? NotFound() : Ok(product);
    }

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(ProductRequest request)
    {
        var product = await _productService.CreateAsync(request);
        return product is null ? BadRequest(new { message = "Category not found." }) : Ok(product);
    }

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductDto>> Update(Guid id, ProductRequest request)
    {
        var product = await _productService.UpdateAsync(id, request);
        return product is null ? NotFound() : Ok(product);
    }

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _productService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("{id:guid}/variants")]
    public async Task<ActionResult<List<ProductVariantDto>>> GetVariants(Guid id) =>
        Ok(await _productService.GetVariantsAsync(id));

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPost("{id:guid}/variants")]
    public async Task<ActionResult<ProductVariantDto>> CreateVariant(Guid id, ProductVariantRequest request)
    {
        var variant = await _productService.CreateVariantAsync(id, request);
        return variant is null ? NotFound(new { message = "Product not found." }) : Ok(variant);
    }

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPut("{id:guid}/variants/{variantId:guid}")]
    public async Task<ActionResult<ProductVariantDto>> UpdateVariant(Guid id, Guid variantId, ProductVariantRequest request)
    {
        var variant = await _productService.UpdateVariantAsync(id, variantId, request);
        return variant is null ? NotFound() : Ok(variant);
    }
}
