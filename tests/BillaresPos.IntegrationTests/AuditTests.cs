using System.Net.Http.Headers;
using System.Net.Http.Json;
using BillaresPos.Api.Auth;
using BillaresPos.Api.Products;
using BillaresPos.Api.Inventory;

namespace BillaresPos.IntegrationTests;

public sealed class AuditTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    [Fact]
    public async Task InventoryAdjustmentCreatesAuditEntry()
    {
        var client = await AuthenticatedClientAsync();
        var productResponse = await client.PostAsJsonAsync("/api/products", new CreateProductRequest("Auditoria inventario", null, null, 0, 1000, 10, 3));
        productResponse.EnsureSuccessStatusCode();
        var product = (await productResponse.Content.ReadFromJsonAsync<ProductResponse>())!;

        var adjustment = await client.PostAsJsonAsync("/api/inventory/" + product.Id + "/adjust",
            new AdjustInventoryRequest(3, BillaresPos.Domain.Inventory.InventoryMovementType.Adjustment, null, "Prueba auditoria"));
        adjustment.EnsureSuccessStatusCode();

        var entries = await client.GetFromJsonAsync<List<AuditEntry>>("/api/audit?page=1&pageSize=100");
        Assert.Contains(entries!, item => item.Action == "INVENTORY_ADJUSTED" && item.EntityType == "InventoryMovement");
    }

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("tenant-a", "owner@tenant-a.local", "TenantA123!"));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private sealed record AuditEntry(Guid Id, Guid? UserId, string Action, string EntityType, Guid? EntityId, string? OldValuesJson, string? NewValuesJson, string? IpAddress, DateTimeOffset CreatedAt);
}
