using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BillaresPos.Api.Auth;
using BillaresPos.Api.Products;
using BillaresPos.Domain.Inventory;

namespace BillaresPos.IntegrationTests;

public sealed class InventoryTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    [Fact]
    public async Task OwnerCanAdjustStockAndReadProjection()
    {
        var client = await AuthenticatedClientAsync("tenant-a", "owner@tenant-a.local", "TenantA123!");

        var response = await client.PostAsJsonAsync($"/api/inventory/{factory.ProductAId}/adjust",
            new AdjustInventoryRequest(12, InventoryMovementType.InitialStock, 2500, "Initial count"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var products = (await client.GetFromJsonAsync<List<ProductResponse>>("/api/products"))!;
        var product = products.Single(x => x.Id == factory.ProductAId);
        Assert.Equal(12, product.CurrentStock);
    }

    [Fact]
    public async Task TenantACannotAdjustTenantBProduct()
    {
        var client = await AuthenticatedClientAsync("tenant-a", "owner@tenant-a.local", "TenantA123!");

        var response = await client.PostAsJsonAsync($"/api/inventory/{factory.ProductBId}/adjust",
            new AdjustInventoryRequest(1, InventoryMovementType.Adjustment, null, "cross tenant"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NegativeAdjustmentCannotCreateNegativeStock()
    {
        var client = await AuthenticatedClientAsync("tenant-a", "owner@tenant-a.local", "TenantA123!");

        var response = await client.PostAsJsonAsync($"/api/inventory/{factory.ProductAId}/adjust",
            new AdjustInventoryRequest(-1, InventoryMovementType.Adjustment, null, "invalid"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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




