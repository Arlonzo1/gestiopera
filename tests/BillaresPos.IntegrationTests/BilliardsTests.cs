using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BillaresPos.Api.Auth;
using BillaresPos.Api.Billiards;
using BillaresPos.Api.CashManagement;
using BillaresPos.Api.Products;
using BillaresPos.Api.Purchases;
using BillaresPos.Domain.Billiards;
using BillaresPos.Domain.Sales;

namespace BillaresPos.IntegrationTests;

public sealed class BilliardsTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task StartTableShowsOccupiedAndActiveSession()
    {
        var client = await AuthenticatedClientAsync();
        var cash = await client.PostAsJsonAsync("/api/cash/open", new OpenCashShiftRequest(0));
        cash.EnsureSuccessStatusCode();
        var shift = (await cash.Content.ReadFromJsonAsync<CashShiftResponse>(JsonOptions))!;
        var table = await CreateTableAsync(client, "Mesa de prueba", 6000, 15);

        var response = await client.PostAsync($"/api/billiards/tables/{table.Id}/start", null);
        response.EnsureSuccessStatusCode();
        var session = (await response.Content.ReadFromJsonAsync<BilliardSessionResponse>(JsonOptions))!;

        var tables = (await client.GetFromJsonAsync<List<BilliardTableResponse>>("/api/billiards/tables", JsonOptions))!;
        var current = tables.Single(x => x.Id == table.Id);
        Assert.Equal(BilliardTableStatus.Occupied, current.Status);
        Assert.Equal(session.SessionId, current.ActiveSessionId);

        var close = await client.PostAsJsonAsync($"/api/cash/{shift.Id}/close", new CloseCashShiftRequest(0));
        close.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task CannotStartTableWithoutOpenCashShift()
    {
        var client = await AuthenticatedClientAsync("tenant-b", "owner@tenant-b.local", "TenantB123!");
        var table = await CreateTableAsync(client, "Mesa sin caja", 6000, 15);
        var response = await client.PostAsync($"/api/billiards/tables/{table.Id}/start", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TenantACannotStartTenantBTable()
    {
        var client = await AuthenticatedClientAsync();

        var response = await client.PostAsync($"/api/billiards/tables/{factory.TableBId}/start", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ClosingSessionCreatesSaleAndDecreasesConsumptionStock()
    {
        var client = await AuthenticatedClientAsync();
        var productResponse = await client.PostAsJsonAsync("/api/products",
            new CreateProductRequest("Bebida de mesa", null, null, 0, 1000, 10, 3));
        productResponse.EnsureSuccessStatusCode();
        var product = (await productResponse.Content.ReadFromJsonAsync<ProductResponse>())!;
        var purchaseResponse = await client.PostAsJsonAsync("/api/purchases",
            new CreatePurchaseRequest(null, null, new DateOnly(2026, 9, 7),
                [new CreatePurchaseItemRequest(product.Id, 5, 600)]));
        purchaseResponse.EnsureSuccessStatusCode();

        var cash = await client.PostAsJsonAsync("/api/cash/open", new OpenCashShiftRequest(0));
        cash.EnsureSuccessStatusCode();
        var table = await CreateTableAsync(client, "Mesa para cierre", 0, 15);
        var start = await client.PostAsync($"/api/billiards/tables/{table.Id}/start", null);
        var started = (await start.Content.ReadFromJsonAsync<BilliardSessionResponse>(JsonOptions))!;
        var consumption = await client.PostAsJsonAsync(
            $"/api/billiards/sessions/{started.SessionId}/consumptions",
            new AddBilliardConsumptionRequest(product.Id, 2));
        consumption.EnsureSuccessStatusCode();

        var close = await client.PostAsJsonAsync($"/api/billiards/sessions/{started.SessionId}/close",
            new CloseBilliardSessionRequest([new BillaresPos.Api.Sales.CreateSalePaymentRequest(PaymentMethod.Cash, 2000)]));
        close.EnsureSuccessStatusCode();
        var closed = (await close.Content.ReadFromJsonAsync<BilliardSessionResponse>(JsonOptions))!;

        Assert.Equal(BilliardSessionStatus.Closed, closed.Status);
        Assert.Equal(2000, closed.Total);
        Assert.NotNull(closed.SaleId);
        var products = (await client.GetFromJsonAsync<List<ProductResponse>>("/api/products"))!;
        Assert.Equal(3, products.Single(x => x.Id == product.Id).CurrentStock);
    }

    private static async Task<BilliardTableResponse> CreateTableAsync(HttpClient client, string name, decimal rate, int increment)
    {
        var response = await client.PostAsJsonAsync("/api/billiards/tables",
            new CreateBilliardTableRequest(name, rate, increment));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BilliardTableResponse>(JsonOptions))!;
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string tenant = "tenant-a", string email = "owner@tenant-a.local", string password = "TenantA123!")
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(tenant, email, password));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}

