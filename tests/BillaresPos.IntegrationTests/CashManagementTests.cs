using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BillaresPos.Api.Auth;
using BillaresPos.Api.CashManagement;
using BillaresPos.Api.Products;
using BillaresPos.Api.Purchases;
using BillaresPos.Api.Sales;
using BillaresPos.Domain.CashManagement;
using BillaresPos.Domain.Sales;

namespace BillaresPos.IntegrationTests;

public sealed class CashManagementTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task CashClosingUsesCashSalesAndCashExpensesOnly()
    {
        var client = await AuthenticatedClientAsync();
        var open = await client.PostAsJsonAsync("/api/cash/open", new OpenCashShiftRequest(10000));
        open.EnsureSuccessStatusCode();
        var shift = (await open.Content.ReadFromJsonAsync<CashShiftResponse>(JsonOptions))!;

        var productResponse = await client.PostAsJsonAsync("/api/products", new CreateProductRequest("Cash test", null, null, 0, 2000, 5, 2));
        var product = (await productResponse.Content.ReadFromJsonAsync<ProductResponse>())!;
        await client.PostAsJsonAsync("/api/purchases", new CreatePurchaseRequest(null, null, new DateOnly(2026, 9, 8), [new CreatePurchaseItemRequest(product.Id, 2, 1000)]));
        var sale = await client.PostAsJsonAsync("/api/sales", new CreateSaleRequest(0, null, [new CreateSaleItemRequest(product.Id, 1)], [new CreateSalePaymentRequest(PaymentMethod.Cash, 2000)]));
        sale.EnsureSuccessStatusCode();

        var expense = await client.PostAsJsonAsync("/api/cash/expenses", new CreateExpenseRequest(300, ExpensePaymentMethod.Cash, "Transporte", "Compra de insumos"));
        expense.EnsureSuccessStatusCode();
        var electronicExpense = await client.PostAsJsonAsync("/api/cash/expenses", new CreateExpenseRequest(700, ExpensePaymentMethod.Nequi, "Servicio", "Pago electronico"));
        electronicExpense.EnsureSuccessStatusCode();

        var close = await client.PostAsJsonAsync($"/api/cash/{shift.Id}/close", new CloseCashShiftRequest(11700));
        close.EnsureSuccessStatusCode();
        var closed = (await close.Content.ReadFromJsonAsync<CashShiftResponse>(JsonOptions))!;

        Assert.Equal(CashShiftStatus.Closed, closed.Status);
        Assert.Equal(11700, closed.ExpectedCash);
        Assert.Equal(0, closed.Difference);
    }

    [Fact]
    public async Task TenantACannotCloseTenantBShift()
    {
        var clientA = await AuthenticatedClientAsync();
        var clientB = await AuthenticatedClientAsync("tenant-b", "owner@tenant-b.local", "TenantB123!");
        var open = await clientB.PostAsJsonAsync("/api/cash/open", new OpenCashShiftRequest(5000));
        var shift = (await open.Content.ReadFromJsonAsync<CashShiftResponse>(JsonOptions))!;

        var response = await clientA.PostAsJsonAsync($"/api/cash/{shift.Id}/close", new CloseCashShiftRequest(5000));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string tenant = "tenant-a", string email = "owner@tenant-a.local", string password = "TenantA123!")
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(tenant, email, password));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}

