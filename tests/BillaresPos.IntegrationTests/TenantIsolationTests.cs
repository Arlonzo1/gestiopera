using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BillaresPos.Api.Auth;

namespace BillaresPos.IntegrationTests;

public sealed class TenantIsolationTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    [Fact]
    public async Task Login_IssuesTokenScopedToTenant()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("tenant-a", "owner@tenant-a.local", "TenantA123!"));

        await EnsureSuccessWithBodyAsync(response);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.Equal(factory.TenantAId, auth!.User.TenantId);
        Assert.Contains("OWNER", auth.User.Roles);
    }

    [Fact]
    public async Task CurrentTenant_ComesFromJwtTenantClaim()
    {
        var client = factory.CreateClient();
        var auth = await LoginAsync(client, "tenant-a", "owner@tenant-a.local", "TenantA123!");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var tenantResponse = await client.GetFromJsonAsync<CurrentTenantResponse>("/api/tenants/current");

        Assert.NotNull(tenantResponse);
        Assert.Equal(factory.TenantAId, tenantResponse!.TenantId);
        Assert.Equal("tenant-a", tenantResponse.Slug);
    }

    [Fact]
    public async Task TenantAOwner_CannotReadTenantBUser()
    {
        var client = factory.CreateClient();
        var auth = await LoginAsync(client, "tenant-a", "owner@tenant-a.local", "TenantA123!");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await client.GetAsync($"/api/users/{factory.OwnerBId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TenantAOwner_CanReadOwnTenantUser()
    {
        var client = factory.CreateClient();
        var auth = await LoginAsync(client, "tenant-a", "owner@tenant-a.local", "TenantA123!");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await client.GetAsync($"/api/users/{factory.OwnerAId}");

        await EnsureSuccessWithBodyAsync(response);
        var user = await response.Content.ReadFromJsonAsync<UserProfile>();
        Assert.NotNull(user);
        Assert.Equal(factory.TenantAId, user!.TenantId);
    }

    private static async Task<AuthResponse> LoginAsync(HttpClient client, string tenantSlug, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(tenantSlug, email, password));
        await EnsureSuccessWithBodyAsync(response);
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private static async Task EnsureSuccessWithBodyAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Expected success but got {(int)response.StatusCode} {response.StatusCode}: {body}");
        }
    }

    private sealed record CurrentTenantResponse(Guid TenantId, string Slug, string TimeZoneId, string Currency);
}

