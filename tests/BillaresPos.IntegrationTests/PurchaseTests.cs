using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BillaresPos.Api.Auth;
using BillaresPos.Api.Products;
using BillaresPos.Api.Purchases;

namespace BillaresPos.IntegrationTests;

public sealed class PurchaseTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    [Fact]
    public async Task PurchaseUpdatesStockAndWeightedAverageCost()
    {
        var client = await AuthenticatedClientAsync("tenant-a", "owner@tenant-a.local", "TenantA123!");
        var product = await CreateProductAsync(client, "Purchase average test");

        var response = await client.PostAsJsonAsync("/api/purchases", new CreatePurchaseRequest(
            "Supplier A", "INV-001", new DateOnly(2026, 9, 7),
            [
                new CreatePurchaseItemRequest(product.Id, 10, 1000),
                new CreatePurchaseItemRequest(product.Id, 5, 2000)
            ]));

        response.EnsureSuccessStatusCode();
        var purchase = (await response.Content.ReadFromJsonAsync<PurchaseResponse>())!;
        Assert.Equal(20000, purchase.Total);
        Assert.Equal(2, purchase.ItemCount);

        var products = (await client.GetFromJsonAsync<List<ProductResponse>>("/api/products"))!;
        var updated = products.Single(x => x.Id == product.Id);
        Assert.Equal(15, updated.CurrentStock);
        Assert.Equal(1333.33m, updated.AverageCost);
    }

    [Fact]
    public async Task InvalidPurchaseDoesNotChangeStock()
    {
        var client = await AuthenticatedClientAsync("tenant-a", "owner@tenant-a.local", "TenantA123!");
        var product = await CreateProductAsync(client, "Purchase rollback test");

        var response = await client.PostAsJsonAsync("/api/purchases", new CreatePurchaseRequest(
            null, null, new DateOnly(2026, 9, 7),
            [
                new CreatePurchaseItemRequest(product.Id, 10, 1000),
                new CreatePurchaseItemRequest(Guid.NewGuid(), 1, 1000)
            ]));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var products = (await client.GetFromJsonAsync<List<ProductResponse>>("/api/products"))!;
        var unchanged = products.Single(x => x.Id == product.Id);
        Assert.Equal(0, unchanged.CurrentStock);
        Assert.Equal(0, unchanged.AverageCost);
    }

    [Fact]
    public async Task TenantACannotPurchaseTenantBProduct()
    {
        var client = await AuthenticatedClientAsync("tenant-a", "owner@tenant-a.local", "TenantA123!");

        var response = await client.PostAsJsonAsync("/api/purchases", new CreatePurchaseRequest(
            null, null, new DateOnly(2026, 9, 7),
            [new CreatePurchaseItemRequest(factory.ProductBId, 1, 1000)]));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<ProductResponse> CreateProductAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/products", new CreateProductRequest(
            name, null, null, 0, 5000, 10, 3));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string tenant, string email, string password)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(tenant, email, password));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}



