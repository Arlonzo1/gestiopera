namespace BillaresPos.Api.Auth;

public sealed record LoginRequest(string? TenantSlug, string Email, string Password);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record AuthResponse(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAt, UserProfile User);
public sealed record UserProfile(Guid Id, Guid TenantId, string Email, string FullName, string[] Roles);
