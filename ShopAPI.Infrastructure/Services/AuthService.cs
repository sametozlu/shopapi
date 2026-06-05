using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using ShopAPI.Application;
using ShopAPI.Domain;

namespace ShopAPI.Infrastructure;

public class AuthService : IAuthService
{
    private readonly AppDbContext _dbContext;
    private readonly ITokenService _tokenService;

    public AuthService(AppDbContext dbContext, ITokenService tokenService)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Length < 2)
            throw new ArgumentException("Full name must be at least 2 characters.");
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            throw new ArgumentException("Email is invalid.");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            throw new ArgumentException("Password must be at least 6 characters.");

        var existing = await _dbContext.Users.FirstOrDefaultAsync(x => x.Email == request.Email.ToLower());
        if (existing is not null)
        {
            throw new InvalidOperationException("Email already in use.");
        }

        var role = request.Email.EndsWith("@admin.local", StringComparison.OrdinalIgnoreCase)
            ? UserRole.Admin
            : UserRole.Customer;

        var user = new AppUser
        {
            FullName = request.FullName,
            Email = request.Email.ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = role
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        return _tokenService.IssueTokens(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Email == request.Email.ToLower())
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        return _tokenService.IssueTokens(user);
    }

    public Task<AuthResponse?> RefreshAsync(RefreshTokenRequest request) =>
        _tokenService.RefreshAsync(request.RefreshToken);

    public Task RevokeAsync(RefreshTokenRequest request) =>
        _tokenService.RevokeRefreshTokenAsync(request.RefreshToken);
}
