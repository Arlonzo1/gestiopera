using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BillaresPos.Api.Auth;
using BillaresPos.Application.Common.Abstractions;
using BillaresPos.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BillaresPos.Api.Auth;

public interface ITokenService
{
    Task<AuthResponse> CreateTokenResponseAsync(ApplicationUser user, CancellationToken cancellationToken);
    string HashRefreshToken(string refreshToken);
}

public sealed class TokenService(
    UserManager<ApplicationUser> userManager,
    IOptions<JwtOptions> jwtOptions,
    IClock clock) : ITokenService
{
    public async Task<AuthResponse> CreateTokenResponseAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var options = jwtOptions.Value;
        var roles = await userManager.GetRolesAsync(user);
        var expiresAt = clock.UtcNow.AddMinutes(options.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new("tenant_id", user.TenantId.ToString()),
            new("full_name", user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: clock.UtcNow.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return new AuthResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            refreshToken,
            expiresAt,
            new UserProfile(user.Id, user.TenantId, user.Email ?? string.Empty, user.FullName, roles.ToArray()));
    }

    public string HashRefreshToken(string refreshToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToHexString(bytes);
    }
}

