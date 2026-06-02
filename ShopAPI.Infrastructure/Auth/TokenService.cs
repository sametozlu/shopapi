using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ShopAPI.Application;
using ShopAPI.Domain;

namespace ShopAPI.Infrastructure.Auth;

public class TokenService : ITokenService
{
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public TokenService(AppDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    public AuthResponse IssueTokens(AppUser user)
    {
        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();
        var refreshDays = _configuration.GetValue("Jwt:RefreshTokenDays", 7);

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(refreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(refreshDays)
        });
        _dbContext.SaveChanges();

        var expiresAt = DateTime.UtcNow.AddHours(_configuration.GetValue("Jwt:AccessTokenHours", 1));
        return new AuthResponse(accessToken, refreshToken, user.Email, user.Role.ToString(), expiresAt);
    }

    public async Task<AuthResponse?> RefreshAsync(string refreshToken)
    {
        var hash = HashToken(refreshToken);
        var stored = await _dbContext.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == hash && !x.IsRevoked);

        if (stored is null || stored.ExpiresAt < DateTime.UtcNow || stored.User is null)
            return null;

        stored.IsRevoked = true;
        return IssueTokens(stored.User);
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken)
    {
        var hash = HashToken(refreshToken);
        var stored = await _dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash);
        if (stored is null) return;
        stored.IsRevoked = true;
        await _dbContext.SaveChangesAsync();
    }

    private string GenerateAccessToken(AppUser user)
    {
        var key = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key missing.");
        var issuer = _configuration["Jwt:Issuer"] ?? "ShopAPI";
        var audience = _configuration["Jwt:Audience"] ?? "ShopAPI.Client";
        var hours = _configuration.GetValue("Jwt:AccessTokenHours", 1);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddHours(hours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
