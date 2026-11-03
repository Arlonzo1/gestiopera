using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BillaresPos.Api.Auth;
using BillaresPos.Api.Products;
using BillaresPos.Api.Purchases;
using BillaresPos.Api.Sales;
using BillaresPos.Domain.Sales;

namespace BillaresPos.IntegrationTests;

public sealed class SalesTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    [Fact]
    public async Task SaleDecreasesStockAndAcceptsSplitPayments()
    {
        var client = await AuthenticatedClientAsync();
        var product = await CreateProductAsync(client, "Sale split test", 1000);
        await PurchaseAsync(client, product.Id, 10, 600);

        var response = await client.PostAsJsonAsync("/api/sales", new CreateSaleRequest(
            0, "POS test",
            [new CreateSaleItemRequest(product.Id, 2)],
            [
                new CreateSalePaymentRequest(PaymentMethod.Cash, 1000),
                new CreateSalePaymentRequest(PaymentMethod.Nequi, 1000)
            ]));

        response.EnsureSuccessStatusCode();
        var sale = (await response.Content.ReadFromJsonAsync<SaleResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } }))!;
        Assert.Equal(2000, sale.Total);
        Assert.Equal(SaleStatus.Completed, sale.Status);

        var products = (await client.GetFromJsonAsync<List<ProductResponse>>("/api/products"))!;
        Assert.Equal(8, products.Single(x => x.Id == product.Id).CurrentStock);
    }

    [Fact]
    public async Task InsufficientStockDoesNotCreateSaleOrChangeStock()
    {
        var client = await AuthenticatedClientAsync();
        var product = await CreateProductAsync(client, "Sale rollback test", 1000);
        await PurchaseAsync(client, product.Id, 2, 600);

        var response = await client.PostAsJsonAsync("/api/sales", new CreateSaleRequest(
            0, null,
            [new CreateSaleItemRequest(product.Id, 3)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 3000)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var products = (await client.GetFromJsonAsync<List<ProductResponse>>("/api/products"))!;
        Assert.Equal(2, products.Single(x => x.Id == product.Id).CurrentStock);
    }

    [Fact]
    public async Task CancellingSaleRestoresStockAndPreservesSale()
    {
        var client = await AuthenticatedClientAsync();
        var product = await CreateProductAsync(client, "Sale cancellation test", 1000);
        await PurchaseAsync(client, product.Id, 5, 600);
        var saleResponse = await client.PostAsJsonAsync("/api/sales", new CreateSaleRequest(
            0, null,
            [new CreateSaleItemRequest(product.Id, 2)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 2000)]));
        var sale = (await saleResponse.Content.ReadFromJsonAsync<SaleResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } }))!;

        var cancellation = await client.PostAsJsonAsync($"/api/sales/{sale.Id}/cancel",
            new CancelSaleRequest("Customer requested cancellation"));

        cancellation.EnsureSuccessStatusCode();
        var cancelled = (await cancellation.Content.ReadFromJsonAsync<SaleResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } }))!;
        Assert.Equal(SaleStatus.Cancelled, cancelled.Status);
        var products = (await client.GetFromJsonAsync<List<ProductResponse>>("/api/products"))!;
        Assert.Equal(5, products.Single(x => x.Id == product.Id).CurrentStock);
    }

    [Fact]
    public async Task TenantACannotSellTenantBProduct()
    {
        var client = await AuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("/api/sales", new CreateSaleRequest(
            0, null,
            [new CreateSaleItemRequest(factory.ProductBId, 1)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 1000)]));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }


    [Fact]
    public async Task InvalidPaymentMethodReturnsBadRequest()
    {
        var client = await AuthenticatedClientAsync();
        using var content = new StringContent(
            """{"discount":0,"items":[],"payments":[{"method":"InvalidMethod","amount":1}]}""",
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/api/sales", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<ProductResponse> CreateProductAsync(HttpClient client, string name, decimal price)
    {
        var response = await client.PostAsJsonAsync("/api/products", new CreateProductRequest(
            name, null, null, 0, price, 10, 3));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
    }

    private static async Task PurchaseAsync(HttpClient client, Guid productId, decimal quantity, decimal unitCost)
    {
        var response = await client.PostAsJsonAsync("/api/purchases", new CreatePurchaseRequest(
            null, null, new DateOnly(2026, 9, 7),
            [new CreatePurchaseItemRequest(productId, quantity, unitCost)]));
        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("tenant-a", "owner@tenant-a.local", "TenantA123!"));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}








