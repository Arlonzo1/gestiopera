using System.Net.Http.Headers;
using System.Net.Http.Json;
using BillaresPos.Api.Auth;
using BillaresPos.Api.Products;
using BillaresPos.Api.Purchases;
using BillaresPos.Api.Sales;
using BillaresPos.Domain.Sales;

namespace BillaresPos.IntegrationTests;

public sealed class ReportsTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    [Fact]
    public async Task ReportUsesHistoricalUnitCostAtSale()
    {
        var client = await AuthenticatedClientAsync();
        var product = await CreateProductAsync(client);
        var purchase = await client.PostAsJsonAsync("/api/purchases", new CreatePurchaseRequest(
            null, null, new DateOnly(2026, 9, 8),
            [new CreatePurchaseItemRequest(product.Id, 10, 600)]));
        purchase.EnsureSuccessStatusCode();

        var sale = await client.PostAsJsonAsync("/api/sales", new CreateSaleRequest(
            0, "Report test",
            [new CreateSaleItemRequest(product.Id, 2)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 2000)]));
        sale.EnsureSuccessStatusCode();

        var response = await client.GetFromJsonAsync<ReportResponse>("/api/reports?from=2026-01-01&to=2026-12-31");
        Assert.NotNull(response);
        Assert.Equal(1200, response.Current.CostOfGoodsSold);
        Assert.Equal(800, response.Current.GrossProfit);
    }

    private async Task<ProductResponse> CreateProductAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/products",
            new CreateProductRequest("Reporte historico", null, null, 0, 1000, 10, 3));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
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
