using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ShopAPI.Application;
using ShopAPI.Domain;
using Xunit;

namespace ShopAPI.Tests.Integration;

public class OrderFlowTests : IAsyncLifetime
{
    private readonly ShopApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.SeedAsync();
        _client = _factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Order_Create_Pay_Completes_With_Mock_Provider()
    {
        var token = await LoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var productsResponse = await _client.GetAsync("/api/products?page=1&pageSize=5&isActive=true");
        productsResponse.EnsureSuccessStatusCode();
        var productsPage = await productsResponse.Content.ReadFromJsonAsync<PagedResult<JsonElement>>();
        productsPage!.Items.Should().NotBeEmpty();
        var productId = productsPage.Items[0].GetProperty("id").GetGuid();

        var addCartResponse = await _client.PostAsJsonAsync("/api/cart/items", new { productId, quantity = 1 });
        addCartResponse.EnsureSuccessStatusCode();

        var addressesResponse = await _client.GetAsync("/api/addresses");
        addressesResponse.EnsureSuccessStatusCode();
        var addresses = await addressesResponse.Content.ReadFromJsonAsync<List<AddressDto>>();
        addresses.Should().NotBeEmpty();

        var createOrderResponse = await _client.PostAsJsonAsync("/api/orders", new
        {
            shippingAddressId = addresses![0].Id,
            shippingMethod = ShippingMethod.Standard,
            couponCode = "WELCOME10"
        });
        createOrderResponse.EnsureSuccessStatusCode();
        var order = await createOrderResponse.Content.ReadFromJsonAsync<OrderDto>();
        order!.Status.Should().Be(OrderStatus.Pending);

        var payResponse = await _client.PostAsync($"/api/orders/{order.Id}/pay", null);
        payResponse.EnsureSuccessStatusCode();
        var payResult = await payResponse.Content.ReadFromJsonAsync<PaymentStartResponse>();
        payResult!.Mode.Should().Be("completed");
        payResult.Order!.Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public async Task Order_Cancel_Restores_Product_Stock()
    {
        var token = await LoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var productsResponse = await _client.GetAsync("/api/products?page=1&pageSize=5&isActive=true");
        var productsPage = await productsResponse.Content.ReadFromJsonAsync<PagedResult<JsonElement>>();
        var productId = productsPage!.Items[0].GetProperty("id").GetGuid();
        var stockBefore = productsPage.Items[0].GetProperty("stock").GetInt32();

        await _client.PostAsJsonAsync("/api/cart/items", new { productId, quantity = 2 });

        var addresses = await (await _client.GetAsync("/api/addresses")).Content.ReadFromJsonAsync<List<AddressDto>>();
        var createOrderResponse = await _client.PostAsJsonAsync("/api/orders", new
        {
            shippingAddressId = addresses![0].Id,
            shippingMethod = ShippingMethod.Standard
        });
        var order = await createOrderResponse.Content.ReadFromJsonAsync<OrderDto>();

        var productAfterOrder = await _client.GetAsync($"/api/products/{productId}");
        var productDto = await productAfterOrder.Content.ReadFromJsonAsync<ProductDto>();
        productDto!.Stock.Should().Be(stockBefore - 2);

        var cancelResponse = await _client.PostAsync($"/api/orders/{order!.Id}/cancel", null);
        cancelResponse.EnsureSuccessStatusCode();
        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<OrderDto>();
        cancelled!.Status.Should().Be(OrderStatus.Cancelled);

        productAfterOrder = await _client.GetAsync($"/api/products/{productId}");
        productDto = await productAfterOrder.Content.ReadFromJsonAsync<ProductDto>();
        productDto!.Stock.Should().Be(stockBefore);
    }

    private async Task<string> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@admin.local", "Admin123!"));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.Token;
    }
}
